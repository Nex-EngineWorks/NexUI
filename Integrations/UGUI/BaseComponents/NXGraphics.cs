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

    /// <summary>
    /// Gradient fill for any uGUI Graphic - linear at an arbitrary angle, or a four-corner blend.
    /// uGUI ships vertex colours but no way to author a gradient without a texture.
    /// </summary>
    [AddComponentMenu("NexUI/Graphics/NX Gradient")]
    public sealed class NXGradient : BaseMeshEffect
    {
        public enum Mode { Linear, FourCorner }

        [SerializeField] private Mode m_Mode = Mode.Linear;
        [SerializeField] private Color m_Start = Color.white;
        [SerializeField] private Color m_End = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField, Range(0f, 360f), Tooltip("Gradient direction in degrees. 90 is bottom to top.")]
        private float m_Angle = 90f;
        [SerializeField] private Color m_TopLeft = Color.white;
        [SerializeField] private Color m_TopRight = Color.white;
        [SerializeField] private Color m_BottomRight = Color.gray;
        [SerializeField] private Color m_BottomLeft = Color.gray;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            var vertices = new System.Collections.Generic.List<UIVertex>();
            vh.GetUIVertexStream(vertices);
            var bounds = Bounds(vertices);
            if (bounds.size.x <= 0f || bounds.size.y <= 0f) return;

            var direction = new Vector2(Mathf.Cos(m_Angle * Mathf.Deg2Rad), Mathf.Sin(m_Angle * Mathf.Deg2Rad));
            for (var i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                var u = Mathf.InverseLerp(bounds.min.x, bounds.max.x, vertex.position.x);
                var v = Mathf.InverseLerp(bounds.min.y, bounds.max.y, vertex.position.y);

                Color sampled;
                if (m_Mode == Mode.FourCorner)
                    sampled = Color.Lerp(Color.Lerp(m_BottomLeft, m_BottomRight, u), Color.Lerp(m_TopLeft, m_TopRight, u), v);
                else
                {
                    // Project the vertex onto the gradient axis so any angle works, not just the axes.
                    var t = Mathf.Clamp01((u - 0.5f) * direction.x + (v - 0.5f) * direction.y + 0.5f);
                    sampled = Color.Lerp(m_Start, m_End, t);
                }

                vertex.color = (Color32)(sampled * (Color)vertex.color);
                vertices[i] = vertex;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(vertices);
        }

        private static Bounds Bounds(System.Collections.Generic.List<UIVertex> vertices)
        {
            var min = vertices[0].position;
            var max = min;
            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex.position);
                max = Vector3.Max(max, vertex.position);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    /// <summary>
    /// A soft drop shadow. uGUI's own Shadow draws a single hard copy; this spreads several fading
    /// copies so the edge actually reads as blurred, which is what UI designs ask for.
    /// </summary>
    [AddComponentMenu("NexUI/Graphics/NX Soft Shadow")]
    public sealed class NXSoftShadow : BaseMeshEffect
    {
        [SerializeField] private Color m_Color = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Vector2 m_Offset = new Vector2(0f, -3f);
        [SerializeField, Range(0f, 32f)] private float m_Spread = 6f;
        [SerializeField, Range(1, 8), Tooltip("Copies used to fake the blur. More is smoother and costs more vertices.")]
        private int m_Steps = 4;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            var vertices = new System.Collections.Generic.List<UIVertex>();
            vh.GetUIVertexStream(vertices);
            var original = new System.Collections.Generic.List<UIVertex>(vertices);

            vh.Clear();
            var shadow = new System.Collections.Generic.List<UIVertex>(original.Count * m_Steps);
            for (var step = m_Steps; step >= 1; step--)
            {
                var t = step / (float)m_Steps;
                var offset = m_Offset + new Vector2(0f, -m_Spread * t * 0.25f);
                var alpha = m_Color.a * (1f - t) / m_Steps * 2f;
                var tint = new Color(m_Color.r, m_Color.g, m_Color.b, alpha);
                var scale = 1f + m_Spread * t * 0.01f;

                foreach (var source in original)
                {
                    var vertex = source;
                    var position = vertex.position;
                    vertex.position = new Vector3(position.x * scale + offset.x, position.y * scale + offset.y, position.z);
                    vertex.color = (Color32)tint;
                    shadow.Add(vertex);
                }
            }

            vh.AddUIVertexTriangleStream(shadow);
            vh.AddUIVertexTriangleStream(original);
        }
    }

    /// <summary>
    /// A bar split into discrete segments - health pips, ammo, shield chunks. Unity's Image fill is
    /// continuous, so games hand-build this every time.
    /// </summary>
    [AddComponentMenu("NexUI/Graphics/NX Segmented Bar")]
    public sealed class NXSegmentedBar : MaskableGraphic
    {
        [SerializeField, Range(1, 64)] private int m_Segments = 5;
        [SerializeField, Range(0f, 1f)] private float m_Value = 1f;
        [SerializeField] private float m_Gap = 4f;
        [SerializeField] private Color m_EmptyColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private bool m_Vertical;
        [SerializeField, Tooltip("Draw the last partially filled segment as a fraction instead of on/off.")]
        private bool m_PartialSegments = true;

        public float Value
        {
            get => m_Value;
            set { m_Value = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        public int Segments
        {
            get => m_Segments;
            set { m_Segments = Mathf.Max(1, value); SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var count = Mathf.Max(1, m_Segments);
            var total = m_Vertical ? rect.height : rect.width;
            var size = (total - m_Gap * (count - 1)) / count;
            if (size <= 0f) return;

            var filled = Mathf.Clamp01(m_Value) * count;
            for (var i = 0; i < count; i++)
            {
                var fill = Mathf.Clamp01(filled - i);
                if (!m_PartialSegments) fill = fill >= 1f ? 1f : 0f;

                var offset = i * (size + m_Gap);
                var slot = m_Vertical
                    ? new Rect(rect.x, rect.y + offset, rect.width, size)
                    : new Rect(rect.x + offset, rect.y, size, rect.height);

                AddQuad(vh, slot, m_EmptyColor);
                if (fill <= 0f) continue;

                var full = m_Vertical
                    ? new Rect(slot.x, slot.y, slot.width, slot.height * fill)
                    : new Rect(slot.x, slot.y, slot.width * fill, slot.height);
                AddQuad(vh, full, color);
            }
        }

        private static void AddQuad(VertexHelper vh, Rect rect, Color32 tint)
        {
            var index = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, new Vector2(1f, 0f));
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }
    }

    /// <summary>
    /// Radial cooldown sweep drawn directly, so an ability icon does not need a second filled Image
    /// child and a material just to darken while recharging.
    /// </summary>
    [AddComponentMenu("NexUI/Graphics/NX Cooldown Overlay")]
    public sealed class NXCooldownOverlay : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f), Tooltip("1 is fully covered (just triggered), 0 is ready.")]
        private float m_Remaining;
        [SerializeField] private bool m_Clockwise = true;
        [SerializeField, Range(8, 64)] private int m_Segments = 32;

        public float Remaining
        {
            get => m_Remaining;
            set { m_Remaining = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (m_Remaining <= 0f) return;

            var rect = GetPixelAdjustedRect();
            var center = new Vector2(rect.center.x, rect.center.y);
            var radius = new Vector2(rect.width, rect.height).magnitude * 0.5f;
            var steps = Mathf.Max(3, Mathf.CeilToInt(m_Segments * m_Remaining));

            vh.AddVert(center, color, new Vector2(0.5f, 0.5f));
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps * m_Remaining;
                var degrees = 90f + (m_Clockwise ? -360f * t : 360f * t);
                var radians = degrees * Mathf.Deg2Rad;
                vh.AddVert(center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius, color, Vector2.zero);
            }

            for (var i = 1; i <= steps; i++)
                vh.AddTriangle(0, i, i + 1);
        }
    }
}
