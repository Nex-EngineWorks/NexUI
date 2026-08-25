using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
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

        public Color ShadowColor { get => m_Color; set { m_Color = value; graphic?.SetVerticesDirty(); } }
        public Vector2 Offset { get => m_Offset; set { m_Offset = value; graphic?.SetVerticesDirty(); } }
        public float Spread { get => m_Spread; set { m_Spread = Mathf.Max(0f, value); graphic?.SetVerticesDirty(); } }

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
}
