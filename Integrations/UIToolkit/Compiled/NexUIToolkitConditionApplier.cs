using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Applies a compiled screen's conditional layers - responsive rules and states - to the
    /// UI Toolkit tree the builder created.
    /// </summary>
    /// <remarks>
    /// The uGUI applier's twin, and the same reasoning holds: one object for both layers because
    /// both write the same properties of the same elements, base + layers rather than a diff so
    /// that a screen which reached Locked by two routes looks the same, and responsive before state
    /// because a state is the more specific statement.
    ///
    /// The property set differs because the targets do. Where uGUI has to reach a
    /// <c>TextMeshProUGUI</c> child for text and colour, here it is the element's own style or its
    /// own text property - which also means a property uGUI can only apply to a node that happens
    /// to own the right component applies to any element here.
    /// </remarks>
    public sealed class NexUIToolkitConditionApplier
    {
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
        private readonly VisualElement[] _built;
        private readonly NexDiagnosticBag _diagnostics;

        private readonly Dictionary<long, NexNodeProperty> _base = new Dictionary<long, NexNodeProperty>();
        private readonly HashSet<string> _reportedUnsupported = new HashSet<string>();
        private readonly List<int> _matchingRules = new List<int>();

        public NexUIToolkitConditionApplier(NexScreenProgram program, VisualElement[] built,
            NexDiagnosticBag diagnostics = null)
        {
            _program = program;
            _built = built;
            _diagnostics = diagnostics;

            CaptureBase();
        }

        public string CurrentStateId { get; private set; }

        public Vector2Int CurrentResolution { get; private set; }

        public UIInputMode CurrentInputMode { get; private set; }

        public bool IsEmpty =>
            _program == null ||
            (_program.States == null || _program.States.IsEmpty) &&
            (_program.Responsive == null || _program.Responsive.IsEmpty);

        public IReadOnlyList<NexStateEntry> States =>
            _program?.States?.States ?? (IReadOnlyList<NexStateEntry>)System.Array.Empty<NexStateEntry>();

        /// <summary>
        /// Applies the screen's starting condition: the panel size and the default state.
        /// </summary>
        /// <remarks>
        /// The panel's size, not <c>Screen</c>. A UI Toolkit screen lives inside a
        /// <c>UIDocument</c> whose panel may be scaled, letterboxed or rendered to a texture, so
        /// the display resolution is the wrong number more often here than in uGUI. When the root
        /// has not been laid out yet the display size is the only answer available, and
        /// <see cref="SetViewport"/> corrects it on the first geometry change.
        /// </remarks>
        public void ApplyInitial(VisualElement root)
        {
            if (IsEmpty) return;

            CurrentResolution = ResolutionOf(root);

            var index = _program.States != null ? _program.States.DefaultIndex() : -1;
            CurrentStateId = index >= 0 ? _program.States.States[index].StateId : null;

            Reapply();
        }

        private static Vector2Int ResolutionOf(VisualElement root)
        {
            if (root != null)
            {
                var size = root.layout.size;
                if (size.x > 0f && size.y > 0f)
                    return new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            }

            return new Vector2Int(Screen.width, Screen.height);
        }

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

        public void SetViewport(Vector2Int resolution, UIInputMode inputMode)
        {
            if (resolution == CurrentResolution && inputMode == CurrentInputMode) return;

            CurrentResolution = resolution;
            CurrentInputMode = inputMode;
            Reapply();
        }

        public void Clear()
        {
            if (IsEmpty) return;

            CurrentStateId = null;
            RestoreBase();
        }

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

        private static long Key(int nodeIndex, string property)
            => ((long)nodeIndex << 32) | (uint)(property ?? string.Empty).GetHashCode();

        private void ApplyDelta(in NexPropertyDelta delta)
        {
            if (!Write(delta.NodeIndex, delta.Value))
                ReportUnsupported(delta.Value.Key);
        }

        private VisualElement ElementAt(int nodeIndex)
            => _built != null && nodeIndex >= 0 && nodeIndex < _built.Length ? _built[nodeIndex] : null;

        private bool TryRead(int nodeIndex, string property, out NexNodeProperty value)
        {
            value = default;
            var element = ElementAt(nodeIndex);
            if (element == null) return false;

            switch (property)
            {
                case Text:
                    value = NexNodeProperty.OfText(Text, TextOf(element));
                    return true;
                case TextColor:
                    value = NexNodeProperty.OfColor(TextColor, element.resolvedStyle.color);
                    return true;
                case FontSize:
                    value = NexNodeProperty.OfNumber(FontSize, element.resolvedStyle.fontSize);
                    return true;
                case Tint:
                    value = NexNodeProperty.OfColor(Tint, element.resolvedStyle.backgroundColor);
                    return true;
                case Opacity:
                    value = NexNodeProperty.OfNumber(Opacity, element.resolvedStyle.opacity);
                    return true;
                case RuntimeVisible:
                    value = NexNodeProperty.OfFlag(RuntimeVisible,
                        element.resolvedStyle.display != DisplayStyle.None);
                    return true;
                case Position:
                    value = NexNodeProperty.OfVector(Position,
                        new Vector2(element.resolvedStyle.left, element.resolvedStyle.top));
                    return true;
                case Width:
                    value = NexNodeProperty.OfNumber(Width, element.resolvedStyle.width);
                    return true;
                case Height:
                    value = NexNodeProperty.OfNumber(Height, element.resolvedStyle.height);
                    return true;
                default:
                    return false;
            }
        }

        private bool Write(int nodeIndex, in NexNodeProperty property)
        {
            var element = ElementAt(nodeIndex);
            if (element == null) return false;

            switch (property.Key)
            {
                case Text:
                    return SetText(element, property.Text ?? string.Empty);
                case TextColor:
                    element.style.color = property.Color;
                    return true;
                case FontSize:
                    element.style.fontSize = property.Number;
                    return true;
                case Tint:
                    element.style.backgroundColor = property.Color;
                    return true;
                case Opacity:
                    element.style.opacity = property.Number;
                    return true;
                case RuntimeVisible:
                    // display rather than visibility: a hidden element must also stop taking part
                    // in its parent's flex layout, or hiding one leaves a hole where it was.
                    element.style.display = property.Flag ? DisplayStyle.Flex : DisplayStyle.None;
                    return true;
                case Position:
                    element.style.left = property.Vector.x;
                    element.style.top = property.Vector.y;
                    return true;
                case Width:
                    element.style.width = property.Number;
                    return true;
                case Height:
                    element.style.height = property.Number;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Reads an element's text whether it is a text element itself or a field that has one.
        /// </summary>
        private static string TextOf(VisualElement element)
        {
            if (element is TextElement text) return text.text ?? string.Empty;

            var child = element.Q<TextElement>();
            return child != null ? child.text ?? string.Empty : string.Empty;
        }

        private static bool SetText(VisualElement element, string value)
        {
            if (element is TextElement text)
            {
                text.text = value;
                return true;
            }

            // A control's label lives in a child, and the author targeted the element rather than
            // its internal structure - the same reach the uGUI surface makes into a Button's label.
            var child = element.Q<TextElement>();
            if (child == null) return false;

            child.text = value;
            return true;
        }

        private void ReportUnsupported(string property)
        {
            if (_diagnostics == null || !_reportedUnsupported.Add(property ?? string.Empty)) return;

            _diagnostics.Add(NexDiagnosticCodes.StateChannelUnsupported,
                new NexSourceLocation(_program.ScreenId, null, null, property),
                "A state or responsive rule changes '" + property + "', which the compiled UI " +
                "Toolkit runtime did not apply - either this backend does not handle that property, " +
                "or the target element has no text to set. The value is in the program and the " +
                "layer's other properties were applied.");
        }
    }
}
