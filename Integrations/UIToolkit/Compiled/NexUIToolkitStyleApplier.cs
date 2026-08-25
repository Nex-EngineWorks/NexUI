using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Applies a compiled node's layout, appearance and typography to a <see cref="VisualElement"/>,
    /// and reports what UI Toolkit cannot express.
    /// </summary>
    /// <remarks>
    /// Three appliers in one file rather than three files mirroring uGUI's, because here they write
    /// one object - <see cref="IStyle"/> - and splitting them would mean three passes over the same
    /// style block arguing about who sets <c>width</c>.
    ///
    /// The interesting half of this class is where the two backends disagree, and they disagree in
    /// both directions. UI Toolkit does natively what uGUI reports as unsupported - wrapping,
    /// maximum size, corner radius, space-between distribution, text outline - and cannot do things
    /// uGUI can: there is no box shadow, no inner shadow and no aspect-ratio fitter. Neither backend
    /// is a subset of the other, which is exactly why the compiler carries everything and each
    /// backend reports its own gaps rather than the compiler pre-filtering to a common denominator.
    /// </remarks>
    public static class NexUIToolkitStyleApplier
    {
        // ---- layout ---------------------------------------------------------

        public static void ApplyLayout(in NexNodeProgram node, VisualElement element,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            var layout = node.Layout;
            if (element == null || layout.IsDefault) return;

            if (layout.ArrangesChildren) ApplyContainer(node, element, authoringPath, diagnostics);
            if (layout.ConstrainsSelf) ApplyChild(node, element, authoringPath, diagnostics);
        }

        private static void ApplyContainer(in NexNodeProgram node, VisualElement element,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            var layout = node.Layout;
            var style = element.style;

            style.flexDirection = layout.Mode == NexLayoutMode.Row
                ? FlexDirection.Row
                : FlexDirection.Column;

            style.paddingLeft = layout.Padding.x;
            style.paddingTop = layout.Padding.y;
            style.paddingRight = layout.Padding.z;
            style.paddingBottom = layout.Padding.w;

            // Native, unlike uGUI, where a wrapping row has no equivalent at all.
            style.flexWrap = layout.Wrap == NexLayoutWrap.Wrap ? Wrap.Wrap : Wrap.NoWrap;

            style.alignItems = AlignFor(layout.Align);
            style.justifyContent = JustifyFor(layout.Justify);

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.LayoutGrid))
                Unsupported(diagnostics, authoringPath, "Grid",
                    "UI Toolkit has no fixed-cell grid container. The children are laid out as a " +
                    "wrapping row of their authored sizes, which matches a grid only when every " +
                    "cell is the same size.", layout: true);
        }

        private static void ApplyChild(in NexNodeProgram node, VisualElement element,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            var layout = node.Layout;
            var style = element.style;

            // Sizing is expressed per axis rather than by one rule, because an author may Hug on
            // one axis while Filling on the other - a label that grows sideways and wraps to its
            // own height is exactly that.
            ApplySizing(layout.WidthSizing, horizontal: true, style);
            ApplySizing(layout.HeightSizing, horizontal: false, style);

            // Native here; uGUI's LayoutElement has no maximum and reports it as unsupported.
            if (layout.MinSize.x > 0f) style.minWidth = layout.MinSize.x;
            if (layout.MinSize.y > 0f) style.minHeight = layout.MinSize.y;
            if (layout.MaxSize.x > 0f) style.maxWidth = layout.MaxSize.x;
            if (layout.MaxSize.y > 0f) style.maxHeight = layout.MaxSize.y;

            if (layout.Margin != Vector4.zero)
            {
                style.marginLeft = layout.Margin.x;
                style.marginTop = layout.Margin.y;
                style.marginRight = layout.Margin.z;
                style.marginBottom = layout.Margin.w;
            }

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.LayoutAspectRatio))
                Unsupported(diagnostics, authoringPath, "Aspect Ratio",
                    "UI Toolkit has no aspect-ratio property, and the width/height pair that would " +
                    "fake it fights whatever the parent's flex layout decides. The element keeps " +
                    "its authored size.", layout: true);
        }

        private static void ApplySizing(NexLayoutSizing sizing, bool horizontal, IStyle style)
        {
            switch (sizing)
            {
                case NexLayoutSizing.Fill:
                    // flexGrow rather than a percentage: the whole point of Fill is "whatever is
                    // left", and a percentage of the parent ignores the siblings.
                    style.flexGrow = 1f;
                    if (horizontal) style.width = StyleKeyword.Auto;
                    else style.height = StyleKeyword.Auto;
                    return;
                case NexLayoutSizing.Hug:
                    if (horizontal) style.width = StyleKeyword.Auto;
                    else style.height = StyleKeyword.Auto;
                    return;
            }
        }

        // ---- appearance -----------------------------------------------------

        public static void ApplyAppearance(in NexNodeProgram node, VisualElement element,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            var appearance = node.Appearance;
            if (element == null || appearance.IsNeutral) return;

            var style = element.style;

            style.opacity = appearance.Opacity;

            if (appearance.BorderWidth > 0f)
            {
                style.borderLeftWidth = appearance.BorderWidth;
                style.borderTopWidth = appearance.BorderWidth;
                style.borderRightWidth = appearance.BorderWidth;
                style.borderBottomWidth = appearance.BorderWidth;
                style.borderLeftColor = appearance.BorderColor;
                style.borderTopColor = appearance.BorderColor;
                style.borderRightColor = appearance.BorderColor;
                style.borderBottomColor = appearance.BorderColor;
            }

            // A real inset border and a real rounded rect, both of which uGUI reports as
            // approximations or as unsupported outright.
            if (appearance.CornerRadius > 0f)
            {
                style.borderTopLeftRadius = appearance.CornerRadius;
                style.borderTopRightRadius = appearance.CornerRadius;
                style.borderBottomLeftRadius = appearance.CornerRadius;
                style.borderBottomRightRadius = appearance.CornerRadius;
            }

            if (appearance.Mask || appearance.Crop) style.overflow = Overflow.Hidden;

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.AppearanceOutline))
                Unsupported(diagnostics, authoringPath, "Outline",
                    "UI Toolkit draws no outline outside an element's box. Use a border, which is " +
                    "inset and is applied.");

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.AppearanceDropShadow))
                Unsupported(diagnostics, authoringPath, "Drop Shadow",
                    "UI Toolkit has no box shadow. uGUI can draw one, so this is a screen that " +
                    "will not look identical on both backends.");

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.AppearanceInnerShadow))
                Unsupported(diagnostics, authoringPath, "Inner Shadow",
                    "Neither backend can draw one; it needs a custom shader or nine-sliced art.");

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.AppearanceBackgroundBlur))
                Unsupported(diagnostics, authoringPath, "Background Blur",
                    "UI Toolkit cannot sample what is behind an element. It needs a render texture " +
                    "the screen is composited over.");
        }

        // ---- typography -----------------------------------------------------

        public static void ApplyTypography(in NexNodeProgram node, VisualElement element,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            var type = node.Typography;
            if (element == null || !type.HasOverrides) return;

            var style = element.style;

            if (type.FontSize > 0f) style.fontSize = type.FontSize;
            style.color = type.Color;
            style.unityFontStyleAndWeight = FontStyleFor(type.Style);
            style.unityTextAlign = AnchorFor(type.Alignment);
            style.whiteSpace = type.Wrapping ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            style.letterSpacing = type.LetterSpacing;
            style.wordSpacing = type.ParagraphSpacing;

            // Native, where uGUI has to route it through a font material preset and reports it.
            if (type.OutlineWidth > 0f)
            {
                style.unityTextOutlineWidth = type.OutlineWidth;
                style.unityTextOutlineColor = type.OutlineColor;
            }

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.TypographyAutoSize))
                Unsupported(diagnostics, authoringPath, "Auto Font Size",
                    "UI Toolkit has no auto-sizing text. The font size is fixed at the authored " +
                    "value; uGUI does resize, so this screen differs between backends.");

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.TypographyEllipsis))
                Unsupported(diagnostics, authoringPath, "Ellipsis",
                    "UI Toolkit clips overflowing text rather than eliding it.");

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.TypographyTextShadow))
                Unsupported(diagnostics, authoringPath, "Text Shadow",
                    "UI Toolkit has no text shadow property.");

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.TypographyRightToLeft))
                Unsupported(diagnostics, authoringPath, "Right To Left",
                    "UI Toolkit has no per-element text direction; the panel's own setting decides.");

            if (NexUnsupported.Applies(node, NexBackendId.UIToolkit, NexCapability.TypographyLineHeight))
                Unsupported(diagnostics, authoringPath, "Line Height",
                    "UI Toolkit has no line-height property. Lines are spaced by the font's own metrics.");
        }

        // ---- shared ---------------------------------------------------------

        private static Align AlignFor(NexLayoutAlignment align)
        {
            switch (align)
            {
                case NexLayoutAlignment.Center: return Align.Center;
                case NexLayoutAlignment.End: return Align.FlexEnd;
                case NexLayoutAlignment.Stretch: return Align.Stretch;
                default: return Align.FlexStart;
            }
        }

        /// <summary>
        /// Main-axis distribution, including the two modes uGUI has to report as unsupported.
        /// </summary>
        private static Justify JustifyFor(NexLayoutJustify justify)
        {
            switch (justify)
            {
                case NexLayoutJustify.Center: return Justify.Center;
                case NexLayoutJustify.End: return Justify.FlexEnd;
                case NexLayoutJustify.SpaceBetween: return Justify.SpaceBetween;
                case NexLayoutJustify.SpaceAround: return Justify.SpaceAround;
                default: return Justify.FlexStart;
            }
        }

        private static FontStyle FontStyleFor(NexFontStyle style)
        {
            bool bold = (style & NexFontStyle.Bold) != 0;
            bool italic = (style & NexFontStyle.Italic) != 0;

            if (bold && italic) return FontStyle.BoldAndItalic;
            if (bold) return FontStyle.Bold;
            if (italic) return FontStyle.Italic;
            return FontStyle.Normal;
        }

        /// <summary>The authoring alignment is a 3x3 grid in reading order, which maps onto TextAnchor.</summary>
        private static TextAnchor AnchorFor(NexTextAlignment alignment)
        {
            switch (alignment)
            {
                case NexTextAlignment.UpperLeft: return TextAnchor.UpperLeft;
                case NexTextAlignment.UpperCenter: return TextAnchor.UpperCenter;
                case NexTextAlignment.UpperRight: return TextAnchor.UpperRight;
                case NexTextAlignment.MiddleLeft: return TextAnchor.MiddleLeft;
                case NexTextAlignment.MiddleRight: return TextAnchor.MiddleRight;
                case NexTextAlignment.LowerLeft: return TextAnchor.LowerLeft;
                case NexTextAlignment.LowerCenter: return TextAnchor.LowerCenter;
                case NexTextAlignment.LowerRight: return TextAnchor.LowerRight;
                default: return TextAnchor.MiddleCenter;
            }
        }

        /// <summary>
        /// Reports through the same two codes the uGUI backend uses.
        /// </summary>
        /// <remarks>
        /// A new "UI Toolkit cannot do this" code would have split one question - "what will this
        /// screen lose on the backend I am shipping?" - across two numbers, and the report already
        /// records which backend raised it.
        /// </remarks>
        private static void Unsupported(NexDiagnosticBag diagnostics, string authoringPath,
            string feature, string detail, bool layout = false)
        {
            diagnostics?.Add(layout
                    ? NexDiagnosticCodes.LayoutFeatureUnsupported
                    : NexDiagnosticCodes.AppearanceFeatureUnsupported,
                new NexSourceLocation(string.Empty, null, authoringPath, feature),
                feature + " is not supported by the UI Toolkit backend. " + detail);
        }
    }
}
