using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// A rounded rectangle that needs no sprite: per-corner radius, an optional inset border, and a
    /// fill - the thing uGUI makes you produce a 9-sliced PNG for.
    /// </summary>
    /// <remarks>
    /// Corners are tessellated into the mesh, so the shape stays crisp at any size and any resolution
    /// and does not consume an atlas slot. Radii are clamped to half the shorter side, which is what
    /// makes a fully rounded "pill" simply a large radius rather than a separate component.
    /// </remarks>
    [AddComponentMenu("NexUI/Graphics/NX Rounded Rect")]
    public sealed class NXRoundedRect : MaskableGraphic
    {
        [SerializeField, Tooltip("Corner radius in pixels, applied to every corner.")]
        private float m_Radius = 12f;
        [SerializeField, Tooltip("Overrides the shared radius per corner when enabled.")]
        private bool m_PerCorner;
        [SerializeField] private float m_TopLeft = 12f;
        [SerializeField] private float m_TopRight = 12f;
        [SerializeField] private float m_BottomRight = 12f;
        [SerializeField] private float m_BottomLeft = 12f;
        [SerializeField, Range(0f, 64f), Tooltip("Inset border thickness. 0 draws a solid shape.")]
        private float m_BorderWidth;
        [SerializeField] private Color m_BorderColor = Color.white;
        [SerializeField, Range(2, 24), Tooltip("Segments per corner. Higher is smoother and costs more vertices.")]
        private int m_CornerSegments = 8;

        public float Radius
        {
            get => m_Radius;
            set { m_Radius = value; SetVerticesDirty(); }
        }

        public float BorderWidth
        {
            get => m_BorderWidth;
            set { m_BorderWidth = value; SetVerticesDirty(); }
        }

        public Color BorderColor
        {
            get => m_BorderColor;
            set { m_BorderColor = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var limit = Mathf.Min(rect.width, rect.height) * 0.5f;

            var tl = Mathf.Clamp(m_PerCorner ? m_TopLeft : m_Radius, 0f, limit);
            var tr = Mathf.Clamp(m_PerCorner ? m_TopRight : m_Radius, 0f, limit);
            var br = Mathf.Clamp(m_PerCorner ? m_BottomRight : m_Radius, 0f, limit);
            var bl = Mathf.Clamp(m_PerCorner ? m_BottomLeft : m_Radius, 0f, limit);

            if (m_BorderWidth <= 0f)
            {
                AddRoundedShape(vh, rect, tl, tr, br, bl, color);
                return;
            }

            // Border first, then the inset fill on top: two solid shapes read identically to a stroked
            // shape and avoid needing a second material or a stencil pass.
            AddRoundedShape(vh, rect, tl, tr, br, bl, m_BorderColor);
            var inset = new Rect(rect.x + m_BorderWidth, rect.y + m_BorderWidth,
                Mathf.Max(0f, rect.width - m_BorderWidth * 2f), Mathf.Max(0f, rect.height - m_BorderWidth * 2f));
            AddRoundedShape(vh, inset,
                Mathf.Max(0f, tl - m_BorderWidth), Mathf.Max(0f, tr - m_BorderWidth),
                Mathf.Max(0f, br - m_BorderWidth), Mathf.Max(0f, bl - m_BorderWidth), color);
        }

        private void AddRoundedShape(VertexHelper vh, Rect rect, float tl, float tr, float br, float bl, Color32 tint)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;

            var center = new Vector2(rect.center.x, rect.center.y);
            var startIndex = vh.currentVertCount;
            vh.AddVert(center, tint, new Vector2(0.5f, 0.5f));

            var outline = new System.Collections.Generic.List<Vector2>(m_CornerSegments * 4 + 4);
            AddCorner(outline, new Vector2(rect.xMax - br, rect.yMin + br), br, -90f, 0f);   // bottom-right
            AddCorner(outline, new Vector2(rect.xMax - tr, rect.yMax - tr), tr, 0f, 90f);    // top-right
            AddCorner(outline, new Vector2(rect.xMin + tl, rect.yMax - tl), tl, 90f, 180f);  // top-left
            AddCorner(outline, new Vector2(rect.xMin + bl, rect.yMin + bl), bl, 180f, 270f); // bottom-left

            foreach (var point in outline)
                vh.AddVert(point, tint, new Vector2(
                    Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                    Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)));

            for (var i = 0; i < outline.Count; i++)
            {
                var current = startIndex + 1 + i;
                var next = startIndex + 1 + (i + 1) % outline.Count;
                vh.AddTriangle(startIndex, current, next);
            }
        }

        private void AddCorner(System.Collections.Generic.List<Vector2> outline, Vector2 pivot, float radius,
            float fromDegrees, float toDegrees)
        {
            if (radius <= 0f) { outline.Add(pivot); return; }
            var steps = Mathf.Max(2, m_CornerSegments);
            for (var i = 0; i <= steps; i++)
            {
                var angle = Mathf.Deg2Rad * Mathf.Lerp(fromDegrees, toDegrees, i / (float)steps);
                outline.Add(pivot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
    }
}
