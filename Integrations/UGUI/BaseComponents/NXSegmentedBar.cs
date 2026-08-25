using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
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
}
