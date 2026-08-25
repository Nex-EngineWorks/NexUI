using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Applies a compiled screen's conditional layers - responsive rules and states - to the uGUI
    /// objects the builder created.
    /// </summary>
    /// <remarks>
    /// One object rather than one per layer, because both write the same properties of the same
    /// nodes. Two independent appliers would each snapshot what it believed the base to be, and the
    /// second one to run would record the first one's output as the base - so returning to the base
    /// would leave the other layer's changes on the screen forever.
    ///
    /// Everything is applied as base + layers, never as a diff against whatever is currently
    /// showing. So the applier snapshots the base value of every property any layer touches, once,
    /// and re-applies from that snapshot on every change. Diffing would be fewer writes and would
    /// make a screen that reached Locked from Selected look different from one that reached Locked
    /// from Normal.
    ///
    /// Layer order is responsive, then state. Responsive says what the device needs; a state says
    /// what this element is doing right now, which is the more specific statement, so it wins.
    ///
    /// Only the properties this backend can genuinely change at runtime are applied. The rest are
    /// in the compiled program - the compiler is backend-neutral and carries them - and are
    /// reported once per property through <see cref="NexDiagnosticCodes.StateChannelUnsupported"/>,
    /// because a Selected state that quietly changes nothing is indistinguishable from a broken one.
    ///
    /// The snapshot covers only the nodes and properties the layers actually mention. Snapshotting
    /// the whole screen would copy hundreds of nodes to be able to restore three.
    /// </remarks>
    public sealed class NexUGuiConditionApplier
    {
        /// <summary>Authoring property paths this backend knows how to change at runtime.</summary>
        /// <remarks>
        /// Deliberately a short list rather than everything the authoring model can express. Each
        /// entry below is a property that has one obvious uGUI component to write to and reads back
        /// the same way, which is what makes restoring the base value exact.
        /// </remarks>
        private const string Text = "text";
        private const string TextColor = "textColor";
        private const string Tint = "tint";
        private const string FontSize = "fontSize";
        private const string RuntimeVisible = "runtimeVisible";
        private const string Opacity = "opacity";
        private const string Position = "position";
        private const string Width = "width";
        private const string Height = "height";

        private readonly NexScreenProgram _program;
        private readonly RectTransform[] _built;
        private readonly NexDiagnosticBag _diagnostics;

        /// <summary>Base value of each (node, property) any layer touches, captured before the first change.</summary>
        private readonly Dictionary<long, NexNodeProperty> _base = new Dictionary<long, NexNodeProperty>();

        private readonly HashSet<string> _reportedUnsupported = new HashSet<string>();

        /// <summary>Reused across re-applies so switching states allocates nothing.</summary>
        private readonly List<int> _matchingRules = new List<int>();

        public NexUGuiConditionApplier(NexScreenProgram program, RectTransform[] built,
            NexDiagnosticBag diagnostics = null)
        {
            _program = program;
            _built = built;
            _diagnostics = diagnostics;

            CaptureBase();
        }

        /// <summary>The state currently applied, or null when the screen is showing its base.</summary>
        public string CurrentStateId { get; private set; }

        /// <summary>The screen size the responsive rules were last evaluated against.</summary>
        public Vector2Int CurrentResolution { get; private set; }

        public UIInputMode CurrentInputMode { get; private set; }

        /// <summary>True when the screen declares neither states nor responsive rules.</summary>
        public bool IsEmpty =>
            _program == null ||
            (_program.States == null || _program.States.IsEmpty) &&
            (_program.Responsive == null || _program.Responsive.IsEmpty);

        /// <summary>Every state this screen declares, in authored order.</summary>
        /// <remarks>
        /// Exposed so a caller can ask what exists before choosing: a slot that shows Locked only
        /// when the game says so still has to know Locked is a state this screen has.
        /// </remarks>
        public IReadOnlyList<NexStateEntry> States =>
            _program?.States?.States ?? (IReadOnlyList<NexStateEntry>)System.Array.Empty<NexStateEntry>();

        /// <summary>
        /// Applies the screen's starting condition: the current screen size and the default state.
        /// </summary>
        /// <remarks>
        /// Separate from the constructor so the caller decides when it happens. The builder calls it
        /// after the build loop, once every node exists.
        /// </remarks>
        public void ApplyInitial()
        {
            if (IsEmpty) return;

            CurrentResolution = new Vector2Int(Screen.width, Screen.height);

            var index = _program.States != null ? _program.States.DefaultIndex() : -1;
            CurrentStateId = index >= 0 ? _program.States.States[index].StateId : null;

            Reapply();
        }

        /// <summary>
        /// Switches the screen to a state. Returns false when the screen declares no such state.
        /// </summary>
        /// <remarks>
        /// Returns false rather than throwing: a state id usually arrives from game code or from a
        /// data-driven rule, and a screen that stops working because a name was misspelled is worse
        /// than one that stays in the state it was in and says so.
        /// </remarks>
        public bool SetState(string stateId)
        {
            if (_program?.States == null) return false;

            if (_program.States.IndexOf(stateId) < 0)
            {
                _diagnostics?.Add(NexDiagnosticCodes.StateChannelUnsupported,
                    new NexSourceLocation(_program.ScreenId, null, null, "state"),
                    "This screen has no state '" + stateId + "'. It stayed in '" +
                    (CurrentStateId ?? "(base)") + "'.");
                return false;
            }

            CurrentStateId = stateId;
            Reapply();
            return true;
        }

        /// <summary>
        /// Tells the screen what it is being displayed on, so responsive rules can re-evaluate.
        /// </summary>
        /// <remarks>
        /// Pushed in by the caller rather than polled from <c>Screen</c> every frame. The size that
        /// matters is the one the screen is laid out in, which is not always the display - a split
        /// view, a render texture and an editor preview are all cases where polling would give the
        /// wrong answer confidently.
        /// </remarks>
        public void SetViewport(Vector2Int resolution, UIInputMode inputMode)
        {
            if (resolution == CurrentResolution && inputMode == CurrentInputMode) return;

            CurrentResolution = resolution;
            CurrentInputMode = inputMode;
            Reapply();
        }

        /// <summary>Returns the screen to its compiled base appearance and forgets the state.</summary>
        public void Clear()
        {
            if (IsEmpty) return;

            CurrentStateId = null;
            RestoreBase();
        }

        // ---- layering -------------------------------------------------------

        /// <summary>
        /// Rebuilds the screen from the base snapshot upwards.
        /// </summary>
        /// <remarks>
        /// Always from the base, never incrementally from the current appearance. A rule that
        /// stopped matching has to be undone, and the only thing that knows what it overwrote is
        /// the snapshot.
        /// </remarks>
        private void Reapply()
        {
            if (IsEmpty) return;

            RestoreBase();

            var responsive = _program.Responsive;
            if (responsive != null && !responsive.IsEmpty)
            {
                responsive.CollectMatching(CurrentResolution, CurrentInputMode, _matchingRules);
                for (int i = 0; i < _matchingRules.Count; i++)
                    foreach (var delta in responsive.DeltasFor(_matchingRules[i]))
                        ApplyDelta(delta);
            }

            if (CurrentStateId == null || _program.States == null) return;

            int stateIndex = _program.States.IndexOf(CurrentStateId);
            if (stateIndex < 0) return;

            foreach (var delta in _program.States.DeltasFor(stateIndex))
                ApplyDelta(delta);
        }

        // ---- base capture and restore ---------------------------------------

        private void CaptureBase()
        {
            if (IsEmpty || _built == null) return;

            if (_program.States != null) CaptureBase(_program.States.Deltas);
            if (_program.Responsive != null) CaptureBase(_program.Responsive.Deltas);
        }

        private void CaptureBase(List<NexPropertyDelta> deltas)
        {
            for (int i = 0; i < deltas.Count; i++)
            {
                var delta = deltas[i];
                var key = Key(delta.NodeIndex, delta.Value.Key);
                if (_base.ContainsKey(key)) continue;

                if (TryRead(delta.NodeIndex, delta.Value.Key, out var current))
                    _base[key] = current;
            }
        }

        private void RestoreBase()
        {
            foreach (var pair in _base)
                Write((int)(pair.Key >> 32), pair.Value);
        }

        /// <summary>
        /// Node index and property name packed into one dictionary key.
        /// </summary>
        /// <remarks>
        /// The property's hash rather than its text, so the key is a value type and the lookup
        /// allocates nothing. A collision would restore the wrong base value, but both halves come
        /// from the same compiled program, so a collision is a compile-time-fixed set of strings
        /// colliding - not attacker input.
        /// </remarks>
        private static long Key(int nodeIndex, string property)
            => ((long)nodeIndex << 32) | (uint)(property ?? string.Empty).GetHashCode();

        // ---- per-property read and write ------------------------------------

        private void ApplyDelta(in NexPropertyDelta delta)
        {
            if (!Write(delta.NodeIndex, delta.Value))
                ReportUnsupported(delta.Value.Key);
        }

        private RectTransform RectAt(int nodeIndex)
            => _built != null && nodeIndex >= 0 && nodeIndex < _built.Length ? _built[nodeIndex] : null;

        private bool TryRead(int nodeIndex, string property, out NexNodeProperty value)
        {
            value = default;
            var rect = RectAt(nodeIndex);
            if (rect == null) return false;

            switch (property)
            {
                case Text:
                {
                    var text = rect.GetComponentInChildren<TMP_Text>(true);
                    if (text == null) return false;
                    value = NexNodeProperty.OfText(Text, text.text);
                    return true;
                }
                case TextColor:
                {
                    var text = rect.GetComponentInChildren<TMP_Text>(true);
                    if (text == null) return false;
                    value = NexNodeProperty.OfColor(TextColor, text.color);
                    return true;
                }
                case FontSize:
                {
                    var text = rect.GetComponentInChildren<TMP_Text>(true);
                    if (text == null) return false;
                    value = NexNodeProperty.OfNumber(FontSize, text.fontSize);
                    return true;
                }
                case Tint:
                {
                    var graphic = rect.GetComponent<Graphic>();
                    if (graphic == null) return false;
                    value = NexNodeProperty.OfColor(Tint, graphic.color);
                    return true;
                }
                case Opacity:
                    value = NexNodeProperty.OfNumber(Opacity, GroupOf(rect).alpha);
                    return true;
                case RuntimeVisible:
                    value = NexNodeProperty.OfFlag(RuntimeVisible, rect.gameObject.activeSelf);
                    return true;
                case Position:
                    value = NexNodeProperty.OfVector(Position, rect.anchoredPosition);
                    return true;
                case Width:
                    value = NexNodeProperty.OfNumber(Width, rect.sizeDelta.x);
                    return true;
                case Height:
                    value = NexNodeProperty.OfNumber(Height, rect.sizeDelta.y);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Writes one property, or returns false when this backend cannot.</summary>
        private bool Write(int nodeIndex, in NexNodeProperty property)
        {
            var rect = RectAt(nodeIndex);
            if (rect == null) return false;

            switch (property.Key)
            {
                case Text:
                {
                    var text = rect.GetComponentInChildren<TMP_Text>(true);
                    if (text == null) return false;
                    text.text = property.Text ?? string.Empty;
                    return true;
                }
                case TextColor:
                {
                    var text = rect.GetComponentInChildren<TMP_Text>(true);
                    if (text == null) return false;
                    text.color = property.Color;
                    return true;
                }
                case FontSize:
                {
                    var text = rect.GetComponentInChildren<TMP_Text>(true);
                    if (text == null) return false;
                    text.fontSize = property.Number;
                    return true;
                }
                case Tint:
                {
                    var graphic = rect.GetComponent<Graphic>();
                    if (graphic == null) return false;
                    graphic.color = property.Color;
                    return true;
                }
                case Opacity:
                    GroupOf(rect).alpha = property.Number;
                    return true;
                case RuntimeVisible:
                    rect.gameObject.SetActive(property.Flag);
                    return true;
                case Position:
                    rect.anchoredPosition = property.Vector;
                    return true;
                case Width:
                    rect.sizeDelta = new Vector2(property.Number, rect.sizeDelta.y);
                    return true;
                case Height:
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, property.Number);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The node's <c>CanvasGroup</c>, added on first use.
        /// </summary>
        /// <remarks>
        /// Added lazily rather than by the builder: a group on every node would cost a component
        /// and an extra canvas traversal per node on screens that never fade anything.
        /// </remarks>
        private static CanvasGroup GroupOf(RectTransform rect)
        {
            var group = rect.GetComponent<CanvasGroup>();
            return group != null ? group : rect.gameObject.AddComponent<CanvasGroup>();
        }

        private void ReportUnsupported(string property)
        {
            if (_diagnostics == null || !_reportedUnsupported.Add(property ?? string.Empty)) return;

            _diagnostics.Add(NexDiagnosticCodes.StateChannelUnsupported,
                new NexSourceLocation(_program.ScreenId, null, null, property),
                "A state or responsive rule changes '" + property + "', which the compiled uGUI " +
                "runtime did not apply - either this backend does not handle that property, or the " +
                "target node has no component that carries it (a panel has no text). The value is " +
                "in the program and the layer's other properties were applied.");
        }
    }
}
