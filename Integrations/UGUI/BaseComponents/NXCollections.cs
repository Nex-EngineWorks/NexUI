using System;
using System.Collections.Generic;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    /// <summary>
    /// Paged carousel: snaps to whole pages, supports looping and auto-advance, and reports the page
    /// so indicators can follow. uGUI has ScrollRect but no paging at all.
    /// </summary>
    [AddComponentMenu("NexUI/Data/NX Carousel")]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class NXCarousel : UIBehaviour, IEndDragHandler
    {
        [SerializeField] private bool m_Horizontal = true;
        [SerializeField, Tooltip("Seconds between automatic page changes. 0 disables auto-advance.")]
        private float m_AutoAdvanceSeconds;
        [SerializeField] private bool m_Loop = true;
        [SerializeField, Tooltip("Seconds the snap animation takes.")]
        private float m_SnapDuration = 0.25f;

        [SerializeField] private UnityEvent<int> m_OnPageChanged = new UnityEvent<int>();

        private ScrollRect _scroll;
        private float _timer;
        private float _snapFrom;
        private float _snapTo;
        private float _snapElapsed = -1f;

        public UnityEvent<int> OnPageChanged => m_OnPageChanged;
        public int PageCount => _scroll != null && _scroll.content != null ? _scroll.content.childCount : 0;
        public int CurrentPage { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _scroll = GetComponent<ScrollRect>();
        }

        public void GoTo(int page, bool animate = true)
        {
            var count = PageCount;
            if (count <= 1) return;

            if (m_Loop) page = (page % count + count) % count;
            else page = Mathf.Clamp(page, 0, count - 1);

            CurrentPage = page;
            var target = count == 1 ? 0f : page / (float)(count - 1);

            if (!animate || m_SnapDuration <= 0f)
            {
                SetNormalized(target);
                m_OnPageChanged.Invoke(page);
                return;
            }

            _snapFrom = Normalized();
            _snapTo = target;
            _snapElapsed = 0f;
            m_OnPageChanged.Invoke(page);
        }

        public void Next() => GoTo(CurrentPage + 1);
        public void Previous() => GoTo(CurrentPage - 1);

        public void OnEndDrag(PointerEventData eventData)
        {
            var count = PageCount;
            if (count <= 1) return;
            // Snap to whichever page the drag ended nearest, which is what makes it feel paged rather
            // than free-scrolling.
            GoTo(Mathf.RoundToInt(Normalized() * (count - 1)));
        }

        private void Update()
        {
            if (_snapElapsed >= 0f)
            {
                _snapElapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(_snapElapsed / Mathf.Max(0.0001f, m_SnapDuration));
                SetNormalized(Mathf.Lerp(_snapFrom, _snapTo, Mathf.SmoothStep(0f, 1f, t)));
                if (t >= 1f) _snapElapsed = -1f;
                return;
            }

            if (m_AutoAdvanceSeconds <= 0f) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer < m_AutoAdvanceSeconds) return;
            _timer = 0f;
            Next();
        }

        private float Normalized()
            => m_Horizontal ? _scroll.horizontalNormalizedPosition : 1f - _scroll.verticalNormalizedPosition;

        private void SetNormalized(float value)
        {
            if (m_Horizontal) _scroll.horizontalNormalizedPosition = value;
            else _scroll.verticalNormalizedPosition = 1f - value;
        }
    }

    /// <summary>
    /// Switches which page object is visible when its tab is selected. uGUI has ToggleGroup but no
    /// notion of tab content, so every project wires the show/hide by hand.
    /// </summary>
    [AddComponentMenu("NexUI/Data/NX Tab Group")]
    public sealed class NXTabGroup : UIBehaviour
    {
        [SerializeField, Tooltip("Tab buttons, in order. Each one selects the page at the same index.")]
        private List<Toggle> m_Tabs = new List<Toggle>();
        [SerializeField, Tooltip("Page roots, in the same order as the tabs.")]
        private List<GameObject> m_Pages = new List<GameObject>();
        [SerializeField] private int m_ActiveIndex;
        [SerializeField, Tooltip("Keep pages loaded and only toggle their visibility.")]
        private bool m_KeepPagesAlive = true;

        [SerializeField] private UnityEvent<int> m_OnTabChanged = new UnityEvent<int>();

        public UnityEvent<int> OnTabChanged => m_OnTabChanged;
        public int ActiveIndex => m_ActiveIndex;

        protected override void OnEnable()
        {
            base.OnEnable();
            for (var i = 0; i < m_Tabs.Count; i++)
            {
                var index = i;
                if (m_Tabs[i] == null) continue;
                m_Tabs[i].onValueChanged.AddListener(on => { if (on) Select(index); });
            }
            Select(m_ActiveIndex, notify: false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var tab in m_Tabs)
                if (tab != null) tab.onValueChanged.RemoveAllListeners();
        }

        public void Select(int index, bool notify = true)
        {
            if (m_Pages.Count == 0) return;
            m_ActiveIndex = Mathf.Clamp(index, 0, m_Pages.Count - 1);

            for (var i = 0; i < m_Pages.Count; i++)
            {
                if (m_Pages[i] == null) continue;
                var active = i == m_ActiveIndex;
                if (m_KeepPagesAlive) m_Pages[i].SetActive(active);
                else if (active) m_Pages[i].SetActive(true);
                else m_Pages[i].SetActive(false);
            }

            for (var i = 0; i < m_Tabs.Count; i++)
                if (m_Tabs[i] != null) m_Tabs[i].SetIsOnWithoutNotify(i == m_ActiveIndex);

            if (notify) m_OnTabChanged.Invoke(m_ActiveIndex);
        }
    }
}
