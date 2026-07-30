using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Insets a RectTransform by the device safe area (notch, punch-hole, home indicator, rounded
    /// corners). Unity exposes <see cref="Screen.safeArea"/> but ships no component that applies it,
    /// so every mobile project rewrites this.
    /// </summary>
    [AddComponentMenu("NexUI/Layout/NX Safe Area")]
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class NXSafeArea : UIBehaviour
    {
        [SerializeField] private bool m_Left = true;
        [SerializeField] private bool m_Right = true;
        [SerializeField] private bool m_Top = true;
        [SerializeField] private bool m_Bottom = true;
        [SerializeField, Tooltip("Extra inset applied on top of the device safe area.")]
        private RectOffset m_AdditionalPadding;

        private Rect _appliedSafeArea;
        private Vector2Int _appliedScreen;

        protected override void OnEnable()
        {
            base.OnEnable();
            Apply(force: true);
        }

        private void Update()
        {
            // Safe area changes on rotation and on foldable posture changes, neither of which raises an
            // event, so this polls - cheaply, by comparing the values first.
            Apply(force: false);
        }

        public void Apply(bool force)
        {
            var safeArea = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == _appliedSafeArea && screen == _appliedScreen) return;
            if (screen.x <= 0 || screen.y <= 0) return;

            _appliedSafeArea = safeArea;
            _appliedScreen = screen;

            var min = safeArea.position;
            var max = safeArea.position + safeArea.size;
            min.x /= screen.x; min.y /= screen.y;
            max.x /= screen.x; max.y /= screen.y;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(m_Left ? min.x : 0f, m_Bottom ? min.y : 0f);
            rect.anchorMax = new Vector2(m_Right ? max.x : 1f, m_Top ? max.y : 1f);

            var padding = m_AdditionalPadding;
            rect.offsetMin = padding == null ? Vector2.zero : new Vector2(padding.left, padding.bottom);
            rect.offsetMax = padding == null ? Vector2.zero : new Vector2(-padding.right, -padding.top);
        }
    }

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

    /// <summary>
    /// Places children around a circle or arc - radial menus, ability wheels, orbiting markers.
    /// There is no uGUI equivalent at all.
    /// </summary>
    [AddComponentMenu("NexUI/Layout/NX Radial Layout")]
    public sealed class NXRadialLayoutGroup : LayoutGroup
    {
        [SerializeField] private float m_Radius = 120f;
        [SerializeField, Range(-360f, 360f)] private float m_StartAngle = 90f;
        [SerializeField, Range(-360f, 360f), Tooltip("Sweep covered by all children. 360 spreads them evenly around.")]
        private float m_SweepAngle = 360f;
        [SerializeField, Tooltip("Rotate each child so it faces outward from the centre.")]
        private bool m_RotateChildren;

        public override void CalculateLayoutInputVertical() { }
        public override void SetLayoutHorizontal() => Place();
        public override void SetLayoutVertical() => Place();

        private void Place()
        {
            var count = rectChildren.Count;
            if (count == 0) return;

            // A full sweep must not place the first and last child on top of each other, while a partial
            // arc should reach its end angle exactly.
            var full = Mathf.Abs(Mathf.Abs(m_SweepAngle) - 360f) < 0.01f;
            var divisor = full ? count : Mathf.Max(1, count - 1);

            for (var i = 0; i < count; i++)
            {
                var child = rectChildren[i];
                var angle = m_StartAngle + m_SweepAngle * (i / (float)divisor);
                var radians = angle * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * m_Radius;

                var width = LayoutUtility.GetPreferredWidth(child);
                var height = LayoutUtility.GetPreferredHeight(child);
                var centreX = rectTransform.rect.width * 0.5f;
                var centreY = rectTransform.rect.height * 0.5f;

                SetChildAlongAxis(child, 0, centreX + offset.x - width * 0.5f, width);
                SetChildAlongAxis(child, 1, centreY - offset.y - height * 0.5f, height);

                if (m_RotateChildren)
                    child.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
        }
    }

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
