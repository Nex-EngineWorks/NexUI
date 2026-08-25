using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// Which capabilities a compiled node actually asks for.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="NexBackendCapabilities"/>: that table says what a backend can
    /// do, this says what a screen wants. A gap is the intersection, and both halves have to be
    /// decided in one place or the compile report and the runtime will answer differently.
    ///
    /// Deliberately the only place that reads "does this node use X" out of the program. Each
    /// backend applier still writes the field its own way, but none of them decides <em>whether</em>
    /// the field is in play - that was how the report and the runtime were free to disagree.
    /// </remarks>
    public static class NexCapabilityUse
    {
        /// <summary>Adds every capability this node asks for to <paramref name="into"/>.</summary>
        public static void Collect(in NexNodeProgram node, ICollection<NexCapability> into)
        {
            if (into == null) return;

            CollectLayout(node.Layout, into);
            CollectAppearance(node.Appearance, into);
            CollectTypography(node.Typography, into);

            var style = node.Style;
            if (style.Classes != null && style.Classes.Length > 0) into.Add(NexCapability.StyleClasses);
            if (style.TokenOverrides != null && style.TokenOverrides.Length > 0)
                into.Add(NexCapability.ThemeTokens);

            if (!node.Motion.IsEmpty) into.Add(NexCapability.Motion);
            if (!string.IsNullOrEmpty(node.LocalizationKey)) into.Add(NexCapability.Localization);
        }

        /// <summary>True when this node asks for the capability.</summary>
        /// <remarks>
        /// For an applier deciding whether to report one gap, where building the whole set would
        /// allocate per node on every screen to answer a single question.
        /// </remarks>
        public static bool Uses(in NexNodeProgram node, NexCapability capability)
        {
            switch (capability)
            {
                case NexCapability.LayoutWrap: return node.Layout.Wrap == NexLayoutWrap.Wrap;
                case NexCapability.LayoutMaxSize: return node.Layout.MaxSize != Vector2.zero;
                case NexCapability.LayoutAspectRatio:
                    return !Mathf.Approximately(node.Layout.AspectRatio, 0f);
                case NexCapability.LayoutGrid: return node.Layout.Mode == NexLayoutMode.Grid;
                case NexCapability.LayoutSpaceDistribution:
                    return node.Layout.Justify == NexLayoutJustify.SpaceBetween ||
                           node.Layout.Justify == NexLayoutJustify.SpaceAround;
                case NexCapability.LayoutMargin: return node.Layout.Margin != Vector4.zero;

                case NexCapability.AppearanceCornerRadius:
                    return node.Appearance.CornerRadius > 0f;
                case NexCapability.AppearanceBorder: return node.Appearance.BorderWidth > 0f;
                case NexCapability.AppearanceOutline: return node.Appearance.OutlineWidth > 0f;
                case NexCapability.AppearanceDropShadow: return node.Appearance.DropShadow;
                case NexCapability.AppearanceShadowBlur:
                    return node.Appearance.DropShadow && node.Appearance.ShadowBlur > 0f;
                case NexCapability.AppearanceInnerShadow: return node.Appearance.InnerShadow;
                case NexCapability.AppearanceBackgroundBlur: return node.Appearance.Blur > 0f;
                case NexCapability.AppearanceCrop: return node.Appearance.Crop;

                case NexCapability.TypographyAutoSize:
                    return node.Typography.HasOverrides && node.Typography.AutoSize;
                case NexCapability.TypographyEllipsis:
                    return node.Typography.HasOverrides &&
                           (node.Typography.Ellipsis ||
                            node.Typography.Overflow == NexTextOverflow.Ellipsis);
                case NexCapability.TypographyLineHeight:
                    return node.Typography.HasOverrides &&
                           !Mathf.Approximately(node.Typography.LineHeight, 0f) &&
                           !Mathf.Approximately(node.Typography.LineHeight, 1f);
                case NexCapability.TypographyTextShadow:
                    return node.Typography.HasOverrides && node.Typography.TextShadow;
                case NexCapability.TypographyTextOutline:
                    return node.Typography.HasOverrides && node.Typography.OutlineWidth > 0f;
                case NexCapability.TypographyRightToLeft:
                    return node.Typography.HasOverrides && node.Typography.RightToLeft;
                case NexCapability.TypographyFontWeight:
                    return node.Typography.HasOverrides &&
                           node.Typography.Weight != NexFontWeight.Regular;

                case NexCapability.StyleClasses:
                    return node.Style.Classes != null && node.Style.Classes.Length > 0;
                case NexCapability.ThemeTokens:
                    return node.Style.TokenOverrides != null && node.Style.TokenOverrides.Length > 0;
                case NexCapability.Motion: return !node.Motion.IsEmpty;
                case NexCapability.Localization: return !string.IsNullOrEmpty(node.LocalizationKey);

                default: return false;
            }
        }

        private static void CollectLayout(in NexLayoutProgram layout, ICollection<NexCapability> into)
        {
            if (layout.IsDefault) return;

            if (layout.Wrap == NexLayoutWrap.Wrap) into.Add(NexCapability.LayoutWrap);
            if (layout.MaxSize != Vector2.zero) into.Add(NexCapability.LayoutMaxSize);
            if (!Mathf.Approximately(layout.AspectRatio, 0f)) into.Add(NexCapability.LayoutAspectRatio);
            if (layout.Mode == NexLayoutMode.Grid) into.Add(NexCapability.LayoutGrid);
            if (layout.Justify == NexLayoutJustify.SpaceBetween ||
                layout.Justify == NexLayoutJustify.SpaceAround)
                into.Add(NexCapability.LayoutSpaceDistribution);
            if (layout.Margin != Vector4.zero) into.Add(NexCapability.LayoutMargin);
        }

        private static void CollectAppearance(in NexAppearanceProgram appearance,
            ICollection<NexCapability> into)
        {
            if (appearance.IsNeutral) return;

            if (appearance.CornerRadius > 0f) into.Add(NexCapability.AppearanceCornerRadius);
            if (appearance.BorderWidth > 0f) into.Add(NexCapability.AppearanceBorder);
            if (appearance.OutlineWidth > 0f) into.Add(NexCapability.AppearanceOutline);
            if (appearance.DropShadow)
            {
                into.Add(NexCapability.AppearanceDropShadow);
                if (appearance.ShadowBlur > 0f) into.Add(NexCapability.AppearanceShadowBlur);
            }
            if (appearance.InnerShadow) into.Add(NexCapability.AppearanceInnerShadow);
            if (appearance.Blur > 0f) into.Add(NexCapability.AppearanceBackgroundBlur);
            if (appearance.Crop) into.Add(NexCapability.AppearanceCrop);
        }

        private static void CollectTypography(in NexTypographyProgram type,
            ICollection<NexCapability> into)
        {
            if (!type.HasOverrides) return;

            if (type.AutoSize) into.Add(NexCapability.TypographyAutoSize);
            if (type.Ellipsis || type.Overflow == NexTextOverflow.Ellipsis)
                into.Add(NexCapability.TypographyEllipsis);
            if (!Mathf.Approximately(type.LineHeight, 0f) && !Mathf.Approximately(type.LineHeight, 1f))
                into.Add(NexCapability.TypographyLineHeight);
            if (type.TextShadow) into.Add(NexCapability.TypographyTextShadow);
            if (type.OutlineWidth > 0f) into.Add(NexCapability.TypographyTextOutline);
            if (type.RightToLeft) into.Add(NexCapability.TypographyRightToLeft);
            if (type.Weight != NexFontWeight.Regular) into.Add(NexCapability.TypographyFontWeight);
        }
    }
}
