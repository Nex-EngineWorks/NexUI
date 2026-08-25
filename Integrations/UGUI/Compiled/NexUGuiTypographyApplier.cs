using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using TMPro;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Applies a compiled node's <see cref="NexTypographyProgram"/> to the TextMeshPro component the
    /// builder created, and reports what TMP cannot express.
    /// </summary>
    /// <remarks>
    /// Runs after the text wiring, not before: the base font size and colour come from the node,
    /// and typography is an override layer that has to win. Applying it first would have the base
    /// values overwrite the author's overrides.
    ///
    /// TMP covers most of the model directly - alignment, wrapping, overflow, auto-size, character
    /// and line spacing, rich text, RTL. Font weight is the exception: TMP resolves weight through
    /// a font asset's weight table rather than a numeric property, so a weight without the matching
    /// font asset cannot be honoured, and font assets are not carried in the compiled program.
    /// </remarks>
    public static class NexUGuiTypographyApplier
    {
        public static void Apply(in NexNodeProgram node, GameObject go, string authoringPath,
            NexDiagnosticBag diagnostics)
        {
            var type = node.Typography;
            if (go == null || !type.HasOverrides) return;

            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;

            if (type.FontSize > 0f) text.fontSize = type.FontSize;
            text.color = type.Color;

            text.fontStyle = StyleFor(type.Style);
            text.alignment = AlignmentFor(type.Alignment);
            text.textWrappingMode = type.Wrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.richText = type.RichText;
            text.isRightToLeftText = type.RightToLeft;

            text.enableAutoSizing = type.AutoSize;
            if (type.AutoSize)
            {
                text.fontSizeMin = type.MinFontSize;
                text.fontSizeMax = type.MaxFontSize;
            }

            // TMP's lineSpacing is an additive offset in font units, while the authoring model uses
            // a multiplier - 1.2 meaning "120% of the line". Converting keeps the authored number
            // meaning what the inspector says it means.
            if (type.LineHeight > 0f) text.lineSpacing = (type.LineHeight - 1f) * 100f;
            text.characterSpacing = type.LetterSpacing;
            text.paragraphSpacing = type.ParagraphSpacing;

            text.overflowMode = OverflowFor(type.Overflow, type.Ellipsis);

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.TypographyFontWeight))
                Unsupported(diagnostics, authoringPath, "Font Weight",
                    "TextMeshPro resolves weight through a font asset's weight table, and the " +
                    "compiled program does not carry font assets. Bold is applied through font style.");

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.TypographyTextShadow) ||
                NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.TypographyTextOutline))
                Unsupported(diagnostics, authoringPath, "Text Shadow / Outline",
                    "TextMeshPro draws these from the font material rather than from the component, " +
                    "so they need a material preset the compiled program does not carry.");
        }

        private static FontStyles StyleFor(NexFontStyle style)
        {
            var result = FontStyles.Normal;
            if ((style & NexFontStyle.Bold) != 0) result |= FontStyles.Bold;
            if ((style & NexFontStyle.Italic) != 0) result |= FontStyles.Italic;
            if ((style & NexFontStyle.Underline) != 0) result |= FontStyles.Underline;
            if ((style & NexFontStyle.Strikethrough) != 0) result |= FontStyles.Strikethrough;
            return result;
        }

        /// <summary>
        /// The authoring alignment is a 3x3 grid in reading order, which maps onto TMP's flags.
        /// </summary>
        private static TextAlignmentOptions AlignmentFor(NexTextAlignment alignment) => alignment switch
        {
            NexTextAlignment.UpperLeft => TextAlignmentOptions.TopLeft,
            NexTextAlignment.UpperCenter => TextAlignmentOptions.Top,
            NexTextAlignment.UpperRight => TextAlignmentOptions.TopRight,
            NexTextAlignment.MiddleLeft => TextAlignmentOptions.Left,
            NexTextAlignment.MiddleRight => TextAlignmentOptions.Right,
            NexTextAlignment.LowerLeft => TextAlignmentOptions.BottomLeft,
            NexTextAlignment.LowerCenter => TextAlignmentOptions.Bottom,
            NexTextAlignment.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center
        };

        /// <summary>
        /// Ellipsis is a separate authoring flag but the same TMP setting as overflow, so it wins
        /// when both are set rather than being silently dropped by whichever is checked second.
        /// </summary>
        private static TextOverflowModes OverflowFor(NexTextOverflow overflow, bool ellipsis)
        {
            if (ellipsis) return TextOverflowModes.Ellipsis;
            return overflow switch
            {
                NexTextOverflow.Clip => TextOverflowModes.Masking,
                NexTextOverflow.Ellipsis => TextOverflowModes.Ellipsis,
                NexTextOverflow.Truncate => TextOverflowModes.Truncate,
                _ => TextOverflowModes.Overflow
            };
        }

        private static void Unsupported(NexDiagnosticBag diagnostics, string authoringPath,
            string feature, string detail)
        {
            diagnostics?.Add(NexDiagnosticCodes.AppearanceFeatureUnsupported,
                new NexSourceLocation(string.Empty, null, authoringPath, feature),
                feature + " is not fully supported by the uGUI backend. " + detail);
        }
    }
}
