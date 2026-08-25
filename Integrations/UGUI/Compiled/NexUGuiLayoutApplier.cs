using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Applies a compiled node's <see cref="NexLayoutProgram"/> to a uGUI object, and reports what
    /// uGUI cannot express rather than dropping it.
    /// </summary>
    /// <remarks>
    /// uGUI's layout system is a close but incomplete match for the authoring model:
    /// <c>HorizontalLayoutGroup</c> / <c>VerticalLayoutGroup</c> / <c>GridLayoutGroup</c> cover
    /// direction, spacing, padding and cell size, <c>LayoutElement</c> covers Fill and minimum
    /// size, and <c>ContentSizeFitter</c> covers Hug. Wrapping a row, maximum size and aspect
    /// ratio have no equivalent - <c>AspectRatioFitter</c> exists but fights a layout group rather
    /// than composing with it.
    ///
    /// The rule this follows is the one that stops half-implemented features from looking finished:
    /// anything the backend cannot express is reported through the diagnostics sink, never silently
    /// ignored. A screen that uses Wrap therefore compiles, runs, and tells you the row will not
    /// wrap - instead of running and quietly laying out wrong.
    /// </remarks>
    public static class NexUGuiLayoutApplier
    {
        public static void Apply(in NexNodeProgram node, RectTransform rect, string authoringPath,
            NexDiagnosticBag diagnostics)
        {
            var layout = node.Layout;
            if (rect == null || layout.IsDefault) return;

            if (layout.ArrangesChildren) ApplyContainer(node, rect, authoringPath, diagnostics);
            if (layout.ConstrainsSelf) ApplyChild(node, rect, authoringPath, diagnostics);
        }

        private static void ApplyContainer(in NexNodeProgram node, RectTransform rect,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            var layout = node.Layout;
            var padding = new RectOffset(
                Mathf.RoundToInt(layout.Padding.x), Mathf.RoundToInt(layout.Padding.z),
                Mathf.RoundToInt(layout.Padding.y), Mathf.RoundToInt(layout.Padding.w));

            if (layout.Mode == NexLayoutMode.Grid)
            {
                var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
                grid.padding = padding;
                grid.spacing = new Vector2(layout.Spacing, layout.Spacing);
                grid.cellSize = layout.GridCellSize;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Max(1, layout.GridColumns);
                grid.childAlignment = TextAnchorFor(layout.Align, layout.Justify);
                return;
            }

            HorizontalOrVerticalLayoutGroup group = layout.Mode == NexLayoutMode.Row
                ? rect.gameObject.AddComponent<HorizontalLayoutGroup>()
                : rect.gameObject.AddComponent<VerticalLayoutGroup>();

            group.padding = padding;
            group.spacing = layout.Spacing;
            group.childAlignment = TextAnchorFor(layout.Align, layout.Justify);

            // Stretch is the one cross-axis alignment uGUI expresses as "force expand" rather than
            // as an anchor, so it is set here instead of folding into childAlignment.
            var stretch = layout.Align == NexLayoutAlignment.Stretch;
            group.childForceExpandWidth = layout.Mode == NexLayoutMode.Column && stretch;
            group.childForceExpandHeight = layout.Mode == NexLayoutMode.Row && stretch;
            group.childControlWidth = true;
            group.childControlHeight = true;

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.LayoutWrap))
                Unsupported(diagnostics, authoringPath, "Wrap",
                    "uGUI's layout groups do not wrap; the row will overflow instead of breaking onto a new line.");

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.LayoutSpaceDistribution))
                Unsupported(diagnostics, authoringPath, "Justify",
                    "uGUI layout groups distribute by alignment only; space-between / space-around " +
                    "fall back to the nearest alignment.");
        }

        private static void ApplyChild(in NexNodeProgram node, RectTransform rect,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            var layout = node.Layout;
            if (layout.WidthSizing == NexLayoutSizing.Fill || layout.HeightSizing == NexLayoutSizing.Fill ||
                layout.MinSize != Vector2.zero)
            {
                var element = rect.gameObject.AddComponent<LayoutElement>();
                element.flexibleWidth = layout.WidthSizing == NexLayoutSizing.Fill ? 1f : -1f;
                element.flexibleHeight = layout.HeightSizing == NexLayoutSizing.Fill ? 1f : -1f;
                element.minWidth = layout.MinSize.x > 0f ? layout.MinSize.x : -1f;
                element.minHeight = layout.MinSize.y > 0f ? layout.MinSize.y : -1f;
            }

            if (layout.WidthSizing == NexLayoutSizing.Hug || layout.HeightSizing == NexLayoutSizing.Hug)
            {
                var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = layout.WidthSizing == NexLayoutSizing.Hug
                    ? ContentSizeFitter.FitMode.PreferredSize
                    : ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = layout.HeightSizing == NexLayoutSizing.Hug
                    ? ContentSizeFitter.FitMode.PreferredSize
                    : ContentSizeFitter.FitMode.Unconstrained;
            }

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.LayoutMaxSize))
                Unsupported(diagnostics, authoringPath, "Max Size",
                    "uGUI's LayoutElement has no maximum; the element can grow past the authored bound.");

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.LayoutAspectRatio))
                Unsupported(diagnostics, authoringPath, "Aspect Ratio",
                    "uGUI's AspectRatioFitter competes with a parent layout group rather than " +
                    "composing with it, so the ratio is not applied here.");

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.LayoutMargin))
                Unsupported(diagnostics, authoringPath, "Margin",
                    "uGUI expresses spacing on the parent group, not per child; use the container's " +
                    "spacing and padding instead.");
        }

        /// <summary>
        /// Maps cross-axis alignment plus main-axis justification onto uGUI's single anchor.
        /// </summary>
        /// <remarks>
        /// uGUI has one <c>TextAnchor</c> where the authoring model has two independent axes, so
        /// this is lossy by construction. Justification wins the axis it controls and alignment
        /// wins the other; the combinations uGUI cannot express are reported by the caller rather
        /// than approximated silently.
        /// </remarks>
        private static TextAnchor TextAnchorFor(NexLayoutAlignment align, NexLayoutJustify justify)
        {
            var vertical = justify switch
            {
                NexLayoutJustify.Center => 1,
                NexLayoutJustify.End => 2,
                _ => 0
            };
            var horizontal = align switch
            {
                NexLayoutAlignment.Center => 1,
                NexLayoutAlignment.End => 2,
                _ => 0
            };

            return (TextAnchor)(vertical * 3 + horizontal);
        }

        private static void Unsupported(NexDiagnosticBag diagnostics, string authoringPath,
            string feature, string detail)
        {
            diagnostics?.Add(NexDiagnosticCodes.LayoutFeatureUnsupported,
                new NexSourceLocation(string.Empty, null, authoringPath, feature),
                feature + " is not supported by the uGUI backend. " + detail);
        }
    }
}
