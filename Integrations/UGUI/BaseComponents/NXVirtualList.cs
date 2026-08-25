using System;
using System.Collections.Generic;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Virtualized list for uGUI: builds views only for the rows currently on screen and recycles them
    /// as it scrolls.
    /// </summary>
    /// <remarks>
    /// UI Toolkit ships ListView for exactly this; uGUI ships nothing, so every project that shows a
    /// few thousand rows writes its own pooling ScrollRect. Bind it with
    /// <see cref="SetSource"/> plus a <see cref="BindItem"/> callback, the same shape as ListView's
    /// makeItem/bindItem, so moving between backends does not mean re-thinking the data flow.
    ///
    /// The range arithmetic is <see cref="NXCollectionController"/>'s, shared with
    /// <see cref="NXCollectionView"/> - one virtualization implementation to reason about rather than
    /// two that drift apart. This component stays the small fixed-height case: uniform rows, no
    /// selection, no states. Reach for <see cref="NXCollectionView"/> when a list needs grids,
    /// selection, paging or loading/empty/error states.
    /// </remarks>
    [AddComponentMenu("NexUI/Data/NX Virtual List")]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class NXVirtualList : UIBehaviour
    {
        [SerializeField, Tooltip("Prototype row. Cloned as needed and reused; keep it disabled in the hierarchy.")]
        private RectTransform m_ItemTemplate;
        [SerializeField, Tooltip("Row height in pixels. Uniform rows are what make virtualization cheap.")]
        private float m_ItemHeight = 48f;
        [SerializeField] private float m_Spacing = 4f;
        [SerializeField, Tooltip("Extra rows kept alive above and below the viewport to hide pop-in.")]
        private int m_Overscan = 2;

        private readonly NXCollectionController _controller = new NXCollectionController();
        private readonly List<RectTransform> _pool = new List<RectTransform>();
        private readonly Dictionary<int, RectTransform> _active = new Dictionary<int, RectTransform>();
        private readonly List<int> _stale = new List<int>();
        private ScrollRect _scroll;
        private RectTransform _content;
        private RectTransform _viewport;

        /// <summary>Called to fill a row view with the data at that index.</summary>
        public Action<int, RectTransform> BindItem;

        public int Count => _controller.ItemCount;

        protected override void Awake()
        {
            base.Awake();
            _scroll = GetComponent<ScrollRect>();
            _content = _scroll.content;
            _viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
            if (m_ItemTemplate != null) m_ItemTemplate.gameObject.SetActive(false);

            PushOptions();
            _controller.VisibleRangeChanged += OnRangeChanged;
            _scroll.onValueChanged.AddListener(OnScrolled);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScrolled);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SyncViewport();
        }

        /// <summary>Sets how many items exist. The list itself never holds your data.</summary>
        public void SetSource(int count)
        {
            PushOptions();
            _controller.SetItemCount(Mathf.Max(0, count));
            ResizeContent();
            Rebuild();
        }

        /// <summary>Re-binds the rows currently on screen, for when the underlying data changed in place.</summary>
        public void RefreshVisible()
        {
            foreach (var pair in _active)
                BindItem?.Invoke(pair.Key, pair.Value);
        }

        private void PushOptions()
        {
            _controller.Options = new NXCollectionOptions
            {
                Layout = NXCollectionLayout.Vertical,
                Virtualization = NXVirtualizationMode.FixedSize,
                Selection = NXSelectionMode.None,
                Interactions = NXCollectionInteractions.None,
                ScrollSelectionIntoView = false,
                ItemSize = m_ItemHeight,
                Spacing = m_Spacing,
                Overscan = Mathf.Max(0, m_Overscan)
            };
            SyncViewport();
        }

        private void SyncViewport()
        {
            if (_viewport == null) return;
            var rect = _viewport.rect;
            _controller.SetViewport(rect.height, rect.width);
            ResizeContent();
        }

        private void OnScrolled(Vector2 _)
        {
            if (_content == null) return;
            _controller.SetScrollOffset(Mathf.Max(0f, _content.anchoredPosition.y));
        }

        private void ResizeContent()
        {
            if (_content == null) return;
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, _controller.ContentSize);
        }

        private void Rebuild()
        {
            foreach (var pair in _active) Release(pair.Value);
            _active.Clear();
            _controller.Invalidate();
            OnRangeChanged(_controller.VisibleRange);
        }

        private void OnRangeChanged(NXCollectionRange range)
        {
            if (_content == null || m_ItemTemplate == null) return;

            // Recycle rows that scrolled out before creating any, so the pool stays the size of the
            // viewport rather than the size of the data.
            _stale.Clear();
            foreach (var pair in _active)
                if (!range.Contains(pair.Key)) _stale.Add(pair.Key);
            foreach (var index in _stale)
            {
                Release(_active[index]);
                _active.Remove(index);
            }

            for (var i = range.FirstIndex; i <= range.LastIndex; i++)
            {
                if (_active.ContainsKey(i)) continue;
                var row = Take();
                row.anchoredPosition = new Vector2(0f, -_controller.OffsetOf(i));
                row.sizeDelta = new Vector2(row.sizeDelta.x, m_ItemHeight);
                _active[i] = row;
                BindItem?.Invoke(i, row);
            }
        }

        private RectTransform Take()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var created = Instantiate(m_ItemTemplate, _content);
            created.gameObject.SetActive(true);
            created.anchorMin = new Vector2(0f, 1f);
            created.anchorMax = new Vector2(1f, 1f);
            created.pivot = new Vector2(0.5f, 1f);
            return created;
        }

        private void Release(RectTransform row)
        {
            row.gameObject.SetActive(false);
            _pool.Add(row);
        }
    }
}
