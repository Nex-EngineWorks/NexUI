using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
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

        public Color StartColor { get => m_Start; set { m_Start = value; graphic?.SetVerticesDirty(); } }
        public Color EndColor { get => m_End; set { m_End = value; graphic?.SetVerticesDirty(); } }
        public float Angle { get => m_Angle; set { m_Angle = Mathf.Repeat(value, 360f); graphic?.SetVerticesDirty(); } }

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
}
