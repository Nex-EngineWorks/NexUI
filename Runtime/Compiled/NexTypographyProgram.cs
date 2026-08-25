using System;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    public enum NexFontWeight
    {
        Thin = 0, Light = 1, Regular = 2, Medium = 3, SemiBold = 4, Bold = 5, Black = 6
    }

    [Flags]
    public enum NexFontStyle
    {
        Normal = 0, Bold = 1, Italic = 2, Underline = 4, Strikethrough = 8
    }

    public enum NexTextAlignment
    {
        UpperLeft = 0, UpperCenter = 1, UpperRight = 2,
        MiddleLeft = 3, MiddleCenter = 4, MiddleRight = 5,
        LowerLeft = 6, LowerCenter = 7, LowerRight = 8
    }

    public enum NexTextOverflow
    {
        Overflow = 0, Clip = 1, Ellipsis = 2, Truncate = 3
    }

    /// <summary>
    /// The text-rendering half of a compiled node: everything about how its text is set, beyond the
    /// size and colour the node already carries.
    /// </summary>
    /// <remarks>
    /// The third instance of the same gap. Layout, appearance and typography were all fully
    /// authorable, all honoured by the canvas preview and the two exporters, and all dropped by the
    /// compiler - so a screen with careful type came out of the compiled runtime with default
    /// alignment, no wrapping rules and no auto-size.
    ///
    /// This is an override layer, not a replacement. <see cref="NexNodeProgram.FontSize"/> and
    /// <see cref="NexNodeProgram.TextColor"/> stay the base values every backend already reads;
    /// this struct is applied on top and only when the author actually opened the typography
    /// section, which is what <c>hasOverrides</c> means in the authoring model.
    ///
    /// Font assets are references, so they are reported by the compiler rather than carried, for
    /// the same reason as material and gradient: the content hash deliberately excludes asset
    /// identity.
    /// </remarks>
    [Serializable]
    public struct NexTypographyProgram
    {
        /// <summary>False when the author never opened the typography section; the rest is then meaningless.</summary>
        public bool HasOverrides;

        public NexFontWeight Weight;
        public NexFontStyle Style;

        /// <summary>Overrides <see cref="NexNodeProgram.FontSize"/> when <see cref="HasOverrides"/>.</summary>
        public float FontSize;

        public bool AutoSize;
        public float MinFontSize;
        public float MaxFontSize;

        public NexTextAlignment Alignment;
        public bool Wrapping;
        public NexTextOverflow Overflow;
        public bool Ellipsis;

        public float LineHeight;
        public float LetterSpacing;
        public float ParagraphSpacing;

        public bool RichText;
        public bool RightToLeft;

        /// <summary>Overrides <see cref="NexNodeProgram.TextColor"/> when <see cref="HasOverrides"/>.</summary>
        public Color Color;

        public bool TextShadow;
        public Color ShadowColor;
        public Vector2 ShadowOffset;
        public float OutlineWidth;
        public Color OutlineColor;
    }
}
