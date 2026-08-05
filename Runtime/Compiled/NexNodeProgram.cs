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

        /// <summary>Command id dispatched when this node is clicked, or empty.</summary>
        public string CommandId;

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

        public bool HasText => Kind == NexNodeKind.Label || Kind == NexNodeKind.Button;

        public bool IsClickable => Kind == NexNodeKind.Button;
    }
}
