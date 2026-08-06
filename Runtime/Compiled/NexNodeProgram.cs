using System;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// What a compiled node <em>is</em>, reduced to the shapes every backend can build.
    /// </summary>
    /// <remarks>
    /// This is intentionally much smaller than the authoring component registry. The compiler
    /// lowers a rich authoring type (IconButton, StatBar, Card...) onto one of these plus
    /// properties; the backend then only has to know four constructions instead of the whole
    /// component library. Growing this enum is a real cost - every backend must implement the
    /// new member - so a new authoring type earns a new kind only when no combination of the
    /// existing ones can express it.
    /// </remarks>
    public enum NexNodeKind
    {
        /// <summary>A rect that draws nothing on its own; the default container.</summary>
        Panel = 0,

        /// <summary>A rect with a fill / sprite.</summary>
        Image = 1,

        /// <summary>A rect that draws text.</summary>
        Label = 2,

        /// <summary>A rect that draws a fill, accepts clicks, and may carry a text child.</summary>
        Button = 3
    }

    /// <summary>
    /// What a node can do, independent of what it looks like.
    /// </summary>
    /// <remarks>
    /// The alternative was a <see cref="NexNodeKind"/> member per control - Slider, Toggle,
    /// Dropdown, InputField - which grows an enum every backend has to switch over, for controls
    /// that differ only in which capabilities they have. Flags say the same thing without the
    /// combinatorial growth: a slider and a scrollbar are both "holds a number", and a backend
    /// that can bind a number binds both without knowing either name.
    ///
    /// This mirrors how the authoring model already works - an element is a container of
    /// components, and what it *is* comes from what is attached to it.
    /// </remarks>
    [Flags]
    public enum NexNodeCapabilities
    {
        None = 0,

        /// <summary>Draws text that a binding can write.</summary>
        Text = 1 << 0,

        /// <summary>Accepts a click that dispatches a command.</summary>
        Click = 1 << 1,

        /// <summary>Holds a number a binding can read and write.</summary>
        Value = 1 << 2,

        /// <summary>Its value is on or off rather than a range.</summary>
        BooleanValue = 1 << 3,

        /// <summary>The user can change the value, so a two-way binding has a source.</summary>
        UserEditable = 1 << 4,

        /// <summary>Draws a vector path rather than a rect - see <see cref="NexNodeProgram.Shape"/>.</summary>
        Vector = 1 << 5,

        /// <summary>
        /// Appears over the screen and has an open/close life of its own - modal, popover, tooltip,
        /// toast.
        /// </summary>
        /// <remarks>
        /// A capability rather than a node kind because an overlay is still whatever it already
        /// was: a modal is a panel that also opens and closes. The backend uses this to attach the
        /// component that owns that life; <see cref="NexNodeProgram.ControlId"/> says which one.
        /// </remarks>
        Overlay = 1 << 6
    }

    /// <summary>uGUI anchor presets, mirrored into the compiled program so the runtime never reads authoring types.</summary>
    public enum NexAnchor
    {
        TopLeft = 0,
        Top,
        TopRight,
        Left,
        Center,
        Right,
        BottomLeft,
        Bottom,
        BottomRight,
        Stretch
    }

    /// <summary>
    /// One node of a compiled screen: a flat, backend-neutral instruction for building a single
    /// element. Value-like and Unity-serializable so the whole program is one asset with no
    /// object graph to patch up on load.
    /// </summary>
    /// <remarks>
    /// Hierarchy is expressed as <see cref="ParentIndex"/> into the program's node array rather
    /// than as nested objects or string parent ids. Three reasons: instantiation is a single
    /// forward pass with no lookups (the compiler guarantees a parent precedes its children),
    /// the layout is cache-friendly for large screens, and a corrupt parent reference is
    /// impossible to express - it is either a valid index or the program failed validation.
    /// </remarks>
    [Serializable]
    public struct NexNodeProgram
    {
        /// <summary>Authoring <c>stableId</c> this node came from. The key the source map joins on.</summary>
        public string NodeId;

        /// <summary>Authoring element id; becomes the GameObject / VisualElement name.</summary>
        public string Name;

        /// <summary>Index of the parent node in the program, or -1 for a screen root.</summary>
        public int ParentIndex;

        public NexNodeKind Kind;

        /// <summary>Canvas-space rect, top-left origin, y growing downward (authoring convention).</summary>
        public Rect Rect;

        public NexAnchor Anchor;

        /// <summary>Fill colour for <see cref="NexNodeKind.Image"/> and <see cref="NexNodeKind.Button"/>.</summary>
        public Color Tint;

        public Color TextColor;

        public int FontSize;

        /// <summary>Literal text. Overwritten at runtime when <see cref="TextBindingKey"/> is set.</summary>
        public string Text;

        /// <summary>Initial active state of the built object.</summary>
        public bool Visible;

        /// <summary>State-store key driving this node's text, or empty.</summary>
        public string TextBindingKey;

        /// <summary>Scalar state key driving this node's value - a slider, a progress bar.</summary>
        public string ValueBindingKey;

        /// <summary>State key driving whether this node is shown.</summary>
        public string VisibilityBindingKey;

        /// <summary>State key driving whether this node accepts input.</summary>
        public string InteractableBindingKey;

        /// <summary>State key driving this node's style class.</summary>
        public string ClassBindingKey;

        /// <summary>
        /// Direction of the text and value bindings.
        /// </summary>
        /// <remarks>
        /// Carried per binding rather than per node: a screen commonly reads a label one way and
        /// writes a slider back, and a single node-wide mode would force the author to pick one.
        /// </remarks>
        public State.UIBindingMode TextBindingMode;

        public State.UIBindingMode ValueBindingMode;

        /// <summary>Converter registry keys, or empty for a straight pass-through.</summary>
        public string TextConverterKey;

        public string ValueConverterKey;

        /// <summary>Command id dispatched when this node is clicked, or empty.</summary>
        public string CommandId;

        /// <summary>Whether anything at all is bound. Lets the builder skip the wiring pass.</summary>
        public bool HasAnyBinding =>
            !string.IsNullOrEmpty(TextBindingKey) ||
            !string.IsNullOrEmpty(ValueBindingKey) ||
            !string.IsNullOrEmpty(VisibilityBindingKey) ||
            !string.IsNullOrEmpty(InteractableBindingKey) ||
            !string.IsNullOrEmpty(ClassBindingKey);

        /// <summary>Whether the value binding writes user edits back to the state store.</summary>
        public bool ValueWritesBack =>
            !string.IsNullOrEmpty(ValueBindingKey) &&
            (ValueBindingMode == State.UIBindingMode.TwoWay ||
             ValueBindingMode == State.UIBindingMode.OneWayToSource);

        /// <summary>Whether the text binding writes user edits back to the state store.</summary>
        public bool TextWritesBack =>
            !string.IsNullOrEmpty(TextBindingKey) &&
            (TextBindingMode == State.UIBindingMode.TwoWay ||
             TextBindingMode == State.UIBindingMode.OneWayToSource);

        /// <summary>Stable handle automated tests find this node by, or empty.</summary>
        public string AutomationId;

        /// <summary>
        /// Semantic role, shared with accessibility rather than duplicated.
        /// </summary>
        /// <remarks>
        /// A test looking for "the button" and a screen reader announcing "button" are asking the
        /// same question, so they read the same field. Two role vocabularies would drift, and the
        /// one the author did not happen to fill in would be the one that mattered.
        /// </remarks>
        public Accessibility.AccessibilityRole Role;

        /// <summary>
        /// What assistive technology should announce for this node, when that differs from
        /// <see cref="Text"/>. Empty means the visible text is the announcement.
        /// </summary>
        /// <remarks>
        /// Kept separate rather than overwriting <see cref="Text"/> because the two answer
        /// different questions. An icon-only close button draws "X" and has to announce "Close";
        /// a label already announces itself and duplicating it into a second field is one more
        /// thing to leave stale in translation.
        /// </remarks>
        public string AccessibilityLabel;

        /// <summary>
        /// Position of this node in the screen's focus and reading order, or -1 when it takes
        /// neither focus nor a place in the announcement order.
        /// </summary>
        /// <remarks>
        /// An ordinal rather than the authored up/down/left/right links. Those describe a spatial
        /// graph for gamepad navigation, which is a different question from "what comes next" -
        /// the sequence a screen reader reads and a Tab press follows. The compiler derives this
        /// from document order, so the reading order matches the hierarchy the author sees.
        /// </remarks>
        public int FocusOrder;

        /// <summary>What this node can do. See <see cref="NexNodeCapabilities"/>.</summary>
        public NexNodeCapabilities Capabilities;

        /// <summary>
        /// Registry key of the control to attach - "Slider", "Toggle", "Dropdown" - or empty.
        /// </summary>
        /// <remarks>
        /// A key rather than a Unity type so the program stays backend-neutral: the uGUI builder
        /// maps it to a Slider, and a UI Toolkit builder would map the same key to its own control.
        /// </remarks>
        public string ControlId;

        /// <summary>Range of <see cref="ValueBindingKey"/> for controls that have one.</summary>
        public float ValueMin;

        public float ValueMax;

        /// <summary>
        /// Authored control settings the backend applies after building - see
        /// <see cref="NexNodeProperty"/>. Empty on a node nobody configured.
        /// </summary>
        /// <remarks>
        /// Sparse on purpose: only overridden properties are written, so a screen of defaults
        /// costs nothing here and the compiled asset stays diffable.
        /// </remarks>
        public NexNodeProperty[] ControlProperties;

        /// <summary>
        /// The vector path this node draws, or null when it is a plain rect.
        /// </summary>
        /// <remarks>
        /// A serialized class rather than a struct, but still inline data - Unity writes it into
        /// the program asset, not as a reference to another object. That keeps the property this
        /// type is built around: the program loads with nothing to patch up afterwards.
        ///
        /// Null on almost every node, which is the point. A screen of rects and labels carries no
        /// path data at all, so vector support costs nothing until something uses it.
        /// </remarks>
        [SerializeReference] public Vector.NexVectorShape Shape;

        /// <summary>Whether this node draws a vector path.</summary>
        public bool HasShape => (Capabilities & NexNodeCapabilities.Vector) != 0 && Shape != null;

        /// <summary>Looks up an authored property, or returns false when it was left at its default.</summary>
        public bool TryGetProperty(string key, out NexNodeProperty property)
        {
            var properties = ControlProperties;
            if (properties != null && !string.IsNullOrEmpty(key))
            {
                for (var i = 0; i < properties.Length; i++)
                {
                    if (!string.Equals(properties[i].Key, key, StringComparison.Ordinal)) continue;
                    property = properties[i];
                    return true;
                }
            }

            property = default;
            return false;
        }

        public bool HasText => (Capabilities & NexNodeCapabilities.Text) != 0
                               || Kind == NexNodeKind.Label || Kind == NexNodeKind.Button;

        public bool IsClickable => (Capabilities & NexNodeCapabilities.Click) != 0
                                   || Kind == NexNodeKind.Button;

        /// <summary>Whether this node can hold the value a value binding would drive.</summary>
        public bool HasValue => (Capabilities & NexNodeCapabilities.Value) != 0;

        /// <summary>Whether the user can change it, which is what a two-way binding needs.</summary>
        public bool IsUserEditable => (Capabilities & NexNodeCapabilities.UserEditable) != 0;

        /// <summary>Whether this node opens and closes over the screen rather than sitting in it.</summary>
        public bool IsOverlay => (Capabilities & NexNodeCapabilities.Overlay) != 0;

        /// <summary>Whether this node takes focus and appears in the reading order.</summary>
        public bool IsFocusable => FocusOrder >= 0;

        /// <summary>
        /// What assistive technology announces: the explicit label when set, otherwise the text.
        /// </summary>
        public string AccessibleName =>
            !string.IsNullOrEmpty(AccessibilityLabel) ? AccessibilityLabel
            : HasText ? Text
            : string.Empty;
    }
}
