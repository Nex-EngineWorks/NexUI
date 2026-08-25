using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
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
