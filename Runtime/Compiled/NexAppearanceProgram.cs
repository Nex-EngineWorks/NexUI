using System;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    /// <summary>How an image-like node fits its source rect.</summary>
    public enum NexImageFit
    {
        Stretch = 0,
        Contain = 1,
        Cover = 2,
        Original = 3
    }

    /// <summary>
    /// The appearance half of a compiled node: the effects an author applied on top of its tint.
    /// </summary>
    /// <remarks>
    /// Added for the same reason as <see cref="NexLayoutProgram"/>, and it was the same bug: the
    /// Designer let an author set corner radius, borders, shadows, outlines, blur, masking and
    /// 9-slice; the canvas preview, the prefab writer and the USS generator all honoured them; and
    /// <see cref="NexScreenProgram"/> carried none of it, so the compiled runtime drew flat
    /// rectangles.
    ///
    /// Only value-typed appearance lives here. Material and Gradient are references rather than
    /// values, and the canonical form deliberately excludes asset identity - a guid would make the
    /// content hash change when an unrelated asset is reimported. They are reported as unsupported
    /// by the compiler instead of being carried as something the hash cannot describe.
    ///
    /// Outline and drop shadow are separate fields, not one "border effect": they compose, and a
    /// node can legitimately have both.
    /// </remarks>
    [Serializable]
    public struct NexAppearanceProgram
    {
        /// <summary>Whole-node alpha multiplier. 1 when untouched.</summary>
        public float Opacity;

        public float BorderWidth;
        public Color BorderColor;
        public float CornerRadius;

        public bool DropShadow;
        public Color ShadowColor;
        public Vector2 ShadowOffset;
        public float ShadowBlur;

        /// <summary>Shadow drawn inside the node's own bounds rather than behind it.</summary>
        public bool InnerShadow;

        public float OutlineWidth;
        public Color OutlineColor;

        /// <summary>Background blur radius, zero for none.</summary>
        public float Blur;

        /// <summary>Clips descendants to this node's rect.</summary>
        public bool Mask;

        /// <summary>Draw the sprite 9-sliced rather than stretched.</summary>
        public bool ImageSlice;

        public NexImageFit ImageFit;

        /// <summary>Crop rather than letterbox when the fit leaves spare space.</summary>
        public bool Crop;

        /// <summary>
        /// The value an untouched node compiles to. Opacity is 1 rather than 0, so
        /// <c>default(NexAppearanceProgram)</c> is *not* the neutral value - use this.
        /// </summary>
        public static NexAppearanceProgram Neutral => new NexAppearanceProgram
        {
            Opacity = 1f,
            ImageFit = NexImageFit.Contain
        };

        /// <summary>
        /// True when this node draws nothing beyond its tint, so a backend can skip it and the
        /// canonical form can omit it.
        /// </summary>
        public bool IsNeutral =>
            Mathf.Approximately(Opacity, 1f) &&
            Mathf.Approximately(BorderWidth, 0f) &&
            Mathf.Approximately(CornerRadius, 0f) &&
            !DropShadow && !InnerShadow &&
            Mathf.Approximately(OutlineWidth, 0f) &&
            Mathf.Approximately(Blur, 0f) &&
            !Mask && !ImageSlice && !Crop &&
            ImageFit == NexImageFit.Contain;
    }
}
