using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Lays children out in a row that wraps onto the next line when it runs out of width - the
    /// "flex-wrap" behaviour uGUI's Horizontal/Vertical groups cannot do and GridLayoutGroup only
    /// fakes with fixed cells.
    /// </summary>
    [AddComponentMenu("NexUI/Layout/NX Flow Layout")]
    public sealed class NXFlowLayoutGroup : LayoutGroup
    {
        [SerializeField] private float m_SpacingX = 8f;
        [SerializeField] private float m_SpacingY = 8f;
        [SerializeField, Tooltip("Lay lines out bottom to top instead of top to bottom.")]
        private bool m_ReverseLines;

        private readonly List<float> _lineHeights = new List<float>();

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            var min = 0f;
            for (var i = 0; i < rectChildren.Count; i++)
                min = Mathf.Max(min, LayoutUtility.GetPreferredWidth(rectChildren[i]));
            SetLayoutInputForAxis(min + padding.horizontal, min + padding.horizontal, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            var height = MeasureLines(rectTransform.rect.width, apply: false);
            SetLayoutInputForAxis(height, height, -1, 1);
        }

        public override void SetLayoutHorizontal() => MeasureLines(rectTransform.rect.width, apply: true);

        public override void SetLayoutVertical() => MeasureLines(rectTransform.rect.width, apply: true);

        /// <summary>
        /// Single pass that both measures and (optionally) places, so the measured height can never
        /// disagree with the placement - the classic source of jitter in hand-rolled flow layouts.
        /// </summary>
        private float MeasureLines(float availableWidth, bool apply)
        {
            _lineHeights.Clear();
            var innerWidth = availableWidth - padding.horizontal;
            var x = 0f;
            var lineHeight = 0f;
            var totalHeight = (float)padding.vertical;
            var lineStart = 0;

            void FlushLine(int endExclusive)
            {
                if (apply)
                {
                    var y = padding.top + totalHeight - padding.vertical;
                    var cursor = 0f;
                    for (var i = lineStart; i < endExclusive; i++)
                    {
                        var child = rectChildren[i];
                        var w = LayoutUtility.GetPreferredWidth(child);
                        var h = LayoutUtility.GetPreferredHeight(child);
                        SetChildAlongAxis(child, 0, padding.left + cursor, w);
                        SetChildAlongAxis(child, 1, y, h);
                        cursor += w + m_SpacingX;
                    }
                }
                _lineHeights.Add(lineHeight);
                totalHeight += lineHeight + m_SpacingY;
                lineHeight = 0f;
                x = 0f;
                lineStart = endExclusive;
            }

            for (var i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                var width = LayoutUtility.GetPreferredWidth(child);
                var height = LayoutUtility.GetPreferredHeight(child);

                if (x > 0f && x + width > innerWidth) FlushLine(i);

                x += width + m_SpacingX;
                lineHeight = Mathf.Max(lineHeight, height);
            }
            if (lineStart < rectChildren.Count) FlushLine(rectChildren.Count);

            if (_lineHeights.Count > 0) totalHeight -= m_SpacingY;
            if (m_ReverseLines && apply) ReverseLinePlacement();
            return totalHeight;
        }

        private void ReverseLinePlacement()
        {
            var height = rectTransform.rect.height;
            for (var i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                var y = child.anchoredPosition.y;
                child.anchoredPosition = new Vector2(child.anchoredPosition.x, -(height + y) - child.rect.height);
            }
        }
    }
}
