using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Applies a compiled node's <see cref="NexAppearanceProgram"/> to a uGUI object, and reports
    /// what stock uGUI cannot draw rather than dropping it.
    /// </summary>
    /// <remarks>
    /// uGUI covers a narrow slice of the authoring model without custom shaders:
    /// <c>CanvasGroup</c> gives opacity, <c>Outline</c> and <c>Shadow</c> give those two effects,
    /// <c>RectMask2D</c> gives masking, and <c>Image.type</c> gives 9-slice. Corner radius,
    /// background blur, inner shadow and a real border all need material work that a stock
    /// component does not provide.
    ///
    /// Reporting the rest is the point. A rounded card that silently renders square looks like a
    /// bug in the author's own file; a rounded card that renders square *and says so* is a known
    /// backend limit the author can design around.
    ///
    /// Outline and drop shadow are applied independently, because they are independent effects -
    /// a node may legitimately have both, and collapsing them into one "border" would make the
    /// two authoring fields mean the same thing.
    /// </remarks>
    public static class NexUGuiAppearanceApplier
    {
        public static void Apply(in NexNodeProgram node, RectTransform rect,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            var appearance = node.Appearance;
            if (rect == null || appearance.IsNeutral) return;

            var go = rect.gameObject;

            if (!Mathf.Approximately(appearance.Opacity, 1f))
                go.AddComponent<CanvasGroup>().alpha = Mathf.Clamp01(appearance.Opacity);

            if (appearance.OutlineWidth > 0f)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = appearance.OutlineColor;
                outline.effectDistance = new Vector2(appearance.OutlineWidth, appearance.OutlineWidth);
            }

            if (appearance.DropShadow)
            {
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = appearance.ShadowColor;

                // uGUI's Shadow is a hard offset copy - there is no blur radius on it, so a soft
                // shadow degrades to a hard one rather than to nothing.
                shadow.effectDistance = appearance.ShadowOffset;
                if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.AppearanceShadowBlur))
                    Unsupported(diagnostics, authoringPath, "Shadow Blur",
                        "uGUI's Shadow draws a hard offset copy; the blur radius is ignored.");
            }

            if (appearance.Mask) go.AddComponent<RectMask2D>();

            if (appearance.ImageSlice)
            {
                var image = go.GetComponent<Image>();
                if (image != null) image.type = Image.Type.Sliced;
                else
                    Unsupported(diagnostics, authoringPath, "9-slice",
                        "9-slice needs a sprite; this node draws no image.");
            }

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.AppearanceCornerRadius))
                Unsupported(diagnostics, authoringPath, "Corner Radius",
                    "Stock uGUI has no rounded-rect renderer; use a rounded sprite or the NexUI " +
                    "vector shape instead.");

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.AppearanceBorder))
                Unsupported(diagnostics, authoringPath, "Border",
                    "uGUI has no border on Image. The closest stock effect is Outline, which draws " +
                    "outside the rect rather than inset.");

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.AppearanceInnerShadow))
                Unsupported(diagnostics, authoringPath, "Inner Shadow",
                    "uGUI's Shadow draws behind the graphic; there is no inset variant.");

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.AppearanceBackgroundBlur))
                Unsupported(diagnostics, authoringPath, "Background Blur",
                    "Background blur needs a grab-pass material, which stock uGUI does not ship.");

            if (NexUnsupported.Applies(node, NexBackendId.UGui, NexCapability.AppearanceCrop))
                Unsupported(diagnostics, authoringPath, "Crop",
                    "uGUI fits a sprite with Image.preserveAspect only; cropping needs a mask or a " +
                    "pre-cropped sprite.");
        }

        private static void Unsupported(NexDiagnosticBag diagnostics, string authoringPath,
            string feature, string detail)
        {
            diagnostics?.Add(NexDiagnosticCodes.AppearanceFeatureUnsupported,
                new NexSourceLocation(string.Empty, null, authoringPath, feature),
                feature + " is not supported by the uGUI backend. " + detail);
        }
    }
}
