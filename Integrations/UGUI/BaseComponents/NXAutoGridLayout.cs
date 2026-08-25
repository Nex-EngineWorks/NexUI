using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// A grid that keeps a target column count and derives the cell size from the available width,
    /// instead of GridLayoutGroup's fixed cells that overflow or leave gaps when the panel resizes.
    /// </summary>
    [AddComponentMenu("NexUI/Layout/NX Auto Grid")]
    public sealed class NXAutoGridLayout : LayoutGroup
    {
        [SerializeField, Min(1)] private int m_Columns = 3;
        [SerializeField] private Vector2 m_Spacing = new Vector2(8f, 8f);
        [SerializeField, Tooltip("Height as a multiple of the computed cell width. 1 keeps cells square.")]
        private float m_AspectRatio = 1f;

        private float _cellHeight;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            var columns = Mathf.Max(1, m_Columns);
            var rows = Mathf.CeilToInt(rectChildren.Count / (float)columns);
            var cellWidth = CellWidth(columns);
            _cellHeight = cellWidth * Mathf.Max(0.05f, m_AspectRatio);
            var height = padding.vertical + rows * _cellHeight + Mathf.Max(0, rows - 1) * m_Spacing.y;
            SetLayoutInputForAxis(height, height, -1, 1);
        }

        public override void SetLayoutHorizontal() => Place();
        public override void SetLayoutVertical() => Place();

        private float CellWidth(int columns)
            => Mathf.Max(1f, (rectTransform.rect.width - padding.horizontal - m_Spacing.x * (columns - 1)) / columns);

        private void Place()
        {
            var columns = Mathf.Max(1, m_Columns);
            var cellWidth = CellWidth(columns);
            var cellHeight = cellWidth * Mathf.Max(0.05f, m_AspectRatio);
            _cellHeight = cellHeight;

            for (var i = 0; i < rectChildren.Count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var x = padding.left + column * (cellWidth + m_Spacing.x);
                var y = padding.top + row * (cellHeight + m_Spacing.y);
                SetChildAlongAxis(rectChildren[i], 0, x, cellWidth);
                SetChildAlongAxis(rectChildren[i], 1, y, cellHeight);
            }
        }
    }
}
