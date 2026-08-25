using System;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    /// <summary>How a container arranges its children, or that it does not.</summary>
    public enum NexLayoutMode
    {
        /// <summary>Children keep the absolute rects the author gave them.</summary>
        None = 0,

        /// <summary>Main axis is horizontal.</summary>
        Row = 1,

        /// <summary>Main axis is vertical.</summary>
        Column = 2,

        /// <summary>Fixed-cell grid, wrapping at a column count.</summary>
        Grid = 3
    }

    /// <summary>How a child sizes itself along one axis inside a laid-out container.</summary>
    public enum NexLayoutSizing
    {
        /// <summary>Use the authored rect size.</summary>
        Fixed = 0,

        /// <summary>Shrink to fit content.</summary>
        Hug = 1,

        /// <summary>Grow into the remaining space on that axis.</summary>
        Fill = 2
    }

    /// <summary>Whether a row/column breaks onto another line when it runs out of room.</summary>
    public enum NexLayoutWrap
    {
        NoWrap = 0,
        Wrap = 1
    }

    /// <summary>Cross-axis placement of children.</summary>
    public enum NexLayoutAlignment
    {
        Start = 0,
        Center = 1,
        End = 2,
        Stretch = 3
    }

    /// <summary>Main-axis distribution of children.</summary>
    public enum NexLayoutJustify
    {
        Start = 0,
        Center = 1,
        End = 2,
        SpaceBetween = 3,
        SpaceAround = 4
    }

    /// <summary>
    /// What a node does on one axis when its parent is resized - Figma's constraints model.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>DesignerConstraintMode</c> one-for-one and is numbered to match, so the two never
    /// need a translation table that could drift.
    ///
    /// Distinct from <see cref="NexLayoutSizing"/>, which is about a node inside a laid-out
    /// container. A constraint applies to a node its parent does <em>not</em> arrange: it is what
    /// keeps a close button in the top-right when a dialog gets wider.
    /// </remarks>
    public enum NexConstraintMode
    {
        /// <summary>Pin to the left / top edge.</summary>
        Start = 0,

        /// <summary>Pin to the right / bottom edge.</summary>
        End = 1,

        /// <summary>Stay centred.</summary>
        Center = 2,

        /// <summary>Resize proportionally with the parent.</summary>
        Scale = 3
    }

    /// <summary>
    /// The layout half of a compiled node: how it arranges its children, and how it sizes itself
    /// inside its own parent.
    /// </summary>
    /// <remarks>
    /// Before this existed the compiled program carried only absolute rects. The Designer had full
    /// Auto Layout and min/max/wrap authoring, and the prefab writer and the UXML generator both
    /// honoured it, but <see cref="NexScreenProgram"/> dropped it - so a screen that laid out
    /// correctly on the canvas and in a saved prefab came out of the compiled runtime as fixed
    /// rectangles. Carrying it here is what makes the three paths agree.
    ///
    /// One struct rather than fields spread across <see cref="NexNodeProgram"/>, because layout is
    /// the thing a backend either implements as a unit or reports as unsupported: a backend that
    /// can do Row but not Wrap has to say so about the whole node, not silently honour half of it.
    ///
    /// <see cref="IsDefault"/> exists so the common case - a plain absolutely-positioned node -
    /// costs nothing in the canonical form or in a backend's branching.
    /// </remarks>
    [Serializable]
    public struct NexLayoutProgram
    {
        /// <summary>How this node arranges its own children.</summary>
        public NexLayoutMode Mode;

        /// <summary>Gap between children along the main axis.</summary>
        public float Spacing;

        /// <summary>Inset from this node's own rect, in left/top/right/bottom order.</summary>
        public Vector4 Padding;

        /// <summary>Columns before a <see cref="NexLayoutMode.Grid"/> wraps.</summary>
        public int GridColumns;

        /// <summary>Cell size for <see cref="NexLayoutMode.Grid"/>.</summary>
        public Vector2 GridCellSize;

        public NexLayoutWrap Wrap;
        public NexLayoutAlignment Align;
        public NexLayoutJustify Justify;

        /// <summary>How this node sizes itself inside its parent's layout.</summary>
        public NexLayoutSizing WidthSizing;

        public NexLayoutSizing HeightSizing;

        /// <summary>Lower size bound, or zero on an axis with no bound.</summary>
        public Vector2 MinSize;

        /// <summary>Upper size bound, or zero on an axis with no bound.</summary>
        public Vector2 MaxSize;

        /// <summary>Outset around this node inside its parent's layout, left/top/right/bottom.</summary>
        public Vector4 Margin;

        /// <summary>Width/height ratio to preserve, or zero for none.</summary>
        public float AspectRatio;

        /// <summary>What this node does horizontally when its parent is resized.</summary>
        /// <remarks>
        /// Lives here rather than in its own struct because a backend resolves it together with the
        /// rest of the layout - in uGUI both this and <see cref="WidthSizing"/> end up writing the
        /// same anchors, and splitting them across two contracts is how two appliers start fighting
        /// over one <c>RectTransform</c>.
        /// </remarks>
        public NexConstraintMode HorizontalConstraint;

        /// <summary>What this node does vertically when its parent is resized.</summary>
        public NexConstraintMode VerticalConstraint;

        /// <summary>
        /// True when this node neither arranges children nor constrains itself, so a backend can
        /// skip it entirely and the canonical form can omit it.
        /// </summary>
        public bool IsDefault =>
            Mode == NexLayoutMode.None &&
            WidthSizing == NexLayoutSizing.Fixed && HeightSizing == NexLayoutSizing.Fixed &&
            Wrap == NexLayoutWrap.NoWrap &&
            Align == NexLayoutAlignment.Start && Justify == NexLayoutJustify.Start &&
            MinSize == Vector2.zero && MaxSize == Vector2.zero &&
            Margin == Vector4.zero && Padding == Vector4.zero &&
            Mathf.Approximately(Spacing, 0f) && Mathf.Approximately(AspectRatio, 0f) &&
            !PinsToParent;

        /// <summary>
        /// True when the node does anything other than pin to its parent's top-left on resize.
        /// </summary>
        /// <remarks>
        /// Start/Start is what a node with no constraint authoring means, and it is also what a
        /// plain anchored rect already does - so it costs nothing and stays out of the canonical
        /// form, exactly like the rest of <see cref="IsDefault"/>.
        /// </remarks>
        public bool PinsToParent =>
            HorizontalConstraint != NexConstraintMode.Start ||
            VerticalConstraint != NexConstraintMode.Start;

        /// <summary>True when this node arranges its children rather than letting them sit at their own rects.</summary>
        public bool ArrangesChildren => Mode != NexLayoutMode.None;

        /// <summary>True when the node constrains its own size beyond its authored rect.</summary>
        public bool ConstrainsSelf =>
            WidthSizing != NexLayoutSizing.Fixed || HeightSizing != NexLayoutSizing.Fixed ||
            MinSize != Vector2.zero || MaxSize != Vector2.zero ||
            !Mathf.Approximately(AspectRatio, 0f);
    }
}
