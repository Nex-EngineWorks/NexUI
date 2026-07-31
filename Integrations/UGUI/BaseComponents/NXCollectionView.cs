using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// The uGUI collection: a virtualized, selectable, state-aware ScrollRect that every list-shaped
    /// NexUI component is a configuration of.
    /// </summary>
    /// <remarks>
    /// List, Grid, InventoryGrid, SelectionList and Carousel are not separate implementations - they
    /// are this component with different <see cref="NXCollectionOptions"/> and a different item
    /// template. That is what keeps one virtualization path to debug instead of six.
    ///
    /// Layout arithmetic lives in <see cref="NXCollectionController"/> (no UnityEngine dependency, so
    /// it is unit-tested without a scene); this class only maps that arithmetic onto RectTransforms
    /// and forwards pointer events.
    ///
    /// The component never owns item data. Assign <see cref="Source"/> and <see cref="BindItem"/>;
    /// what an item *is* stays in the game's own model.
    /// </remarks>
    [AddComponentMenu("NexUI/Data/NX Collection View")]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class NXCollectionView : UIBehaviour, INXCollectionView, INXList, INXGrid
    {
        [SerializeField, Tooltip("Prototype item. Cloned as needed and recycled; keep it inactive in the hierarchy.")]
        private RectTransform m_ItemTemplate;

        [SerializeField, Tooltip("Root shown while State is Loading. Optional.")]
        private GameObject m_LoadingView;

        [SerializeField, Tooltip("Root shown while the collection has no items. Optional.")]
        private GameObject m_EmptyView;

        [SerializeField, Tooltip("Root shown while State is Error. Optional.")]
        private GameObject m_ErrorView;

        [SerializeField] private NXCollectionLayout m_Layout = NXCollectionLayout.Vertical;
        [SerializeField] private NXVirtualizationMode m_Virtualization = NXVirtualizationMode.FixedSize;
        [SerializeField] private NXSelectionMode m_Selection = NXSelectionMode.Single;
        [SerializeField] private NXPagingMode m_Paging = NXPagingMode.None;
        [SerializeField] private float m_ItemSize = 64f;
        [SerializeField] private float m_ItemCrossSize = 64f;
        [SerializeField] private float m_Spacing = 4f;
        [SerializeField] private float m_CrossSpacing = 4f;
        [SerializeField] private int m_ColumnCount = 4;
        [SerializeField] private bool m_AutoColumns;
        [SerializeField, Range(0, 8)] private int m_Overscan = 2;
        [SerializeField] private bool m_Activate = true;
        [SerializeField] private bool m_Reorder;
        [SerializeField] private bool m_DragAndDrop;
        [SerializeField] private bool m_ContextRequest;

        private readonly NXCollectionController _controller = new NXCollectionController();
        private readonly Dictionary<int, RectTransform> _realized = new Dictionary<int, RectTransform>();
        private readonly List<RectTransform> _pool = new List<RectTransform>();
        private readonly List<int> _stale = new List<int>();
        private ScrollRect _scroll;
        private RectTransform _content;
        private RectTransform _viewport;
        private INXCollectionSource _source;
        private bool _started;

        /// <summary>Fills an item view with the data at that index. The view is recycled, never fresh.</summary>
        public Action<int, object, RectTransform> BindItem;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public NXCollectionController Controller => _controller;

        /// <inheritdoc/>
        public NXCollectionOptions Options
        {
            get => _controller.Options;
            set
            {
                _controller.Options = value ?? new NXCollectionOptions();
                ApplyOptionsToFields();
                Rebuild();
            }
        }

        /// <inheritdoc/>
        public INXCollectionSource Source
        {
            get => _source;
            set
            {
                if (_source != null) _source.Changed -= OnSourceChanged;
                _source = value;
                if (_source != null) _source.Changed += OnSourceChanged;
                OnSourceChanged();
            }
        }

        /// <inheritdoc/>
        public NXCollectionState State
        {
            get => _controller.State;
            set => _controller.State = value;
        }

        /// <inheritdoc/>
        public int Count => _controller.ItemCount;

        /// <inheritdoc/>
        public int SelectedIndex
        {
            get => _controller.SelectedIndex;
            set => _controller.Select(value);
        }

        /// <inheritdoc/>
        public int ColumnCount
        {
            get => _controller.ColumnCount;
            set
            {
                m_ColumnCount = Mathf.Max(1, value);
                m_AutoColumns = false;
                PushOptions();
            }
        }

        /// <summary>Raised when the selected index changes. <see cref="Controller"/> reports multi-selection.</summary>
        public event Action<int> SelectionChanged;

        /// <summary>The prototype item view. Assigned by the Designer when it writes the prefab.</summary>
        public RectTransform ItemTemplate
        {
            get => m_ItemTemplate;
            set => m_ItemTemplate = value;
        }

        /// <summary>Roots shown for the non-content states. Any of them may be null.</summary>
        public void SetStateViews(GameObject loading, GameObject empty, GameObject error)
        {
            m_LoadingView = loading;
            m_EmptyView = empty;
            m_ErrorView = error;
        }

        /// <summary>
        /// Writes authored options into the serialized fields without touching the live view.
        /// </summary>
        /// <remarks>
        /// The <see cref="Options"/> setter rebuilds the realized window, which is right at runtime
        /// and wrong when an editor tool is writing a prefab that is not playing. This writes the
        /// fields only, so what the Designer saved is what the component deserializes with.
        /// </remarks>
        public void ApplyAuthoredOptions(NXCollectionOptions options)
        {
            if (options == null) return;
            _controller.Options = options;
            ApplyOptionsToFields();
        }

        protected override void Awake()
        {
            base.Awake();
            _scroll = GetComponent<ScrollRect>();
            _content = _scroll.content;
            _viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
            if (m_ItemTemplate != null) m_ItemTemplate.gameObject.SetActive(false);

            _controller.VisibleRangeChanged += OnRangeChanged;
            _controller.SelectionChanged += OnSelectionChanged;
            _controller.StateChanged += OnStateChanged;
            _controller.ScrollRequested += OnScrollRequested;
            PushOptions();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_scroll != null) _scroll.onValueChanged.AddListener(OnScrolled);
            _started = true;
            SyncViewport();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScrolled);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_source != null) _source.Changed -= OnSourceChanged;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (_started) SyncViewport();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (Application.isPlaying && _started) PushOptions();
        }
#endif

        // ---- INXList / INXGrid compatibility -------------------------------------------------

        /// <summary>Sets the items directly, for callers holding a plain list.</summary>
        public void SetItems(IReadOnlyList<object> items)
        {
            var boxed = _source as NXBoxedListSource ?? new NXBoxedListSource();
            if (!ReferenceEquals(boxed, _source)) Source = boxed;
            boxed.Set(items);
        }

        /// <inheritdoc/>
        public void Refresh()
        {
            foreach (var pair in _realized)
                BindItem?.Invoke(pair.Key, _source?.GetItem(pair.Key), pair.Value);
        }

        /// <inheritdoc/>
        public void ScrollTo(int index, NXScrollAlignment alignment = NXScrollAlignment.Nearest)
            => _controller.ScrollTo(index, alignment);

        // ---- Wiring ---------------------------------------------------------------------------

        private void PushOptions()
        {
            var interactions = NXCollectionInteractions.None;
            if (m_Activate) interactions |= NXCollectionInteractions.Activate;
            if (m_Reorder) interactions |= NXCollectionInteractions.Reorder;
            if (m_DragAndDrop) interactions |= NXCollectionInteractions.DragAndDrop;
            if (m_ContextRequest) interactions |= NXCollectionInteractions.ContextRequest;

            _controller.Options = new NXCollectionOptions
            {
                Layout = m_Layout,
                Virtualization = m_Virtualization,
                Selection = m_Selection,
                Paging = m_Paging,
                Interactions = interactions,
                ItemSize = m_ItemSize,
                ItemCrossSize = m_ItemCrossSize,
                Spacing = m_Spacing,
                CrossSpacing = m_CrossSpacing,
                ColumnCount = m_ColumnCount,
                AutoColumns = m_AutoColumns,
                Overscan = m_Overscan
            };

            ConfigureScrollAxis();
            SyncViewport();
        }

        private void ApplyOptionsToFields()
        {
            var options = _controller.Options;
            m_Layout = options.Layout;
            m_Virtualization = options.Virtualization;
            m_Selection = options.Selection;
            m_Paging = options.Paging;
            m_ItemSize = options.ItemSize;
            m_ItemCrossSize = options.ItemCrossSize;
            m_Spacing = options.Spacing;
            m_CrossSpacing = options.CrossSpacing;
            m_ColumnCount = options.ColumnCount;
            m_AutoColumns = options.AutoColumns;
            m_Overscan = options.Overscan;
            m_Activate = (options.Interactions & NXCollectionInteractions.Activate) != 0;
            m_Reorder = (options.Interactions & NXCollectionInteractions.Reorder) != 0;
            m_DragAndDrop = (options.Interactions & NXCollectionInteractions.DragAndDrop) != 0;
            m_ContextRequest = (options.Interactions & NXCollectionInteractions.ContextRequest) != 0;
            ConfigureScrollAxis();
        }

        private bool Horizontal => _controller.Options.Layout == NXCollectionLayout.Horizontal;

        private void ConfigureScrollAxis()
        {
            if (_scroll == null) return;
            _scroll.horizontal = Horizontal;
            _scroll.vertical = !Horizontal;
        }

        private void SyncViewport()
        {
            if (_viewport == null) return;
            var rect = _viewport.rect;
            _controller.SetViewport(Horizontal ? rect.width : rect.height,
                Horizontal ? rect.height : rect.width);
            ResizeContent();
        }

        private void OnSourceChanged()
        {
            _controller.SetItemCount(_source?.Count ?? 0);
            ResizeContent();
            Rebuild();
        }

        private void OnScrolled(Vector2 _)
        {
            if (_content == null) return;
            var offset = Horizontal ? -_content.anchoredPosition.x : _content.anchoredPosition.y;
            _controller.SetScrollOffset(Mathf.Max(0f, offset));
        }

        private void OnScrollRequested(float offset)
        {
            if (_content == null) return;
            var position = _content.anchoredPosition;
            if (Horizontal) position.x = -offset;
            else position.y = offset;
            _content.anchoredPosition = position;
        }

        private void OnStateChanged(NXCollectionState state)
        {
            if (m_LoadingView != null) m_LoadingView.SetActive(state == NXCollectionState.Loading);
            if (m_EmptyView != null) m_EmptyView.SetActive(state == NXCollectionState.Empty);
            if (m_ErrorView != null) m_ErrorView.SetActive(state == NXCollectionState.Error);
            if (_content != null) _content.gameObject.SetActive(state == NXCollectionState.Content);
        }

        private void OnSelectionChanged(IReadOnlyList<int> selection)
        {
            foreach (var pair in _realized)
                ApplySelectionVisual(pair.Key, pair.Value);
            SelectionChanged?.Invoke(_controller.SelectedIndex);
        }

        private void ApplySelectionVisual(int index, RectTransform view)
        {
            // Selection is expressed through Unity's own toggle-ish contract when the template has
            // one, so a project can style it with its existing Selectable transitions instead of a
            // NexUI-specific convention.
            var toggle = view.GetComponent<Toggle>();
            if (toggle != null) toggle.SetIsOnWithoutNotify(_controller.IsSelected(index));
        }

        private void OnRangeChanged(NXCollectionRange range)
        {
            if (m_ItemTemplate == null || _content == null) return;

            _stale.Clear();
            foreach (var pair in _realized)
                if (!range.Contains(pair.Key)) _stale.Add(pair.Key);
            foreach (var index in _stale)
            {
                Release(_realized[index]);
                _realized.Remove(index);
            }

            for (var i = range.FirstIndex; i <= range.LastIndex; i++)
            {
                if (_realized.TryGetValue(i, out var existing))
                {
                    Place(i, existing);
                    continue;
                }
                var view = Take(i);
                _realized[i] = view;
                Place(i, view);
                BindItem?.Invoke(i, _source?.GetItem(i), view);
                ApplySelectionVisual(i, view);
                Measure(i, view);
            }

            ResizeContent();
        }

        private void Rebuild()
        {
            foreach (var pair in _realized) Release(pair.Value);
            _realized.Clear();
            _controller.Invalidate();
            OnRangeChanged(_controller.VisibleRange);
        }

        private void Place(int index, RectTransform view)
        {
            var main = _controller.OffsetOf(index);
            var cross = _controller.CrossOffsetOf(index);
            var crossSize = _controller.CellCrossSize();
            var options = _controller.Options;

            if (Horizontal)
            {
                view.anchorMin = new Vector2(0f, 1f);
                view.anchorMax = new Vector2(0f, 1f);
                view.pivot = new Vector2(0f, 1f);
                view.sizeDelta = new Vector2(options.ItemSize, crossSize);
                view.anchoredPosition = new Vector2(main, -cross);
                return;
            }

            view.anchorMin = new Vector2(0f, 1f);
            view.anchorMax = new Vector2(0f, 1f);
            view.pivot = new Vector2(0f, 1f);
            var width = _controller.ColumnCount > 1 ? crossSize : ContentWidth();
            view.sizeDelta = new Vector2(width, options.ItemSize);
            view.anchoredPosition = new Vector2(cross, -main);
        }

        private float ContentWidth() => _viewport != null ? _viewport.rect.width : 0f;

        private void Measure(int index, RectTransform view)
        {
            if (_controller.Options.Virtualization != NXVirtualizationMode.DynamicSize) return;
            var size = Horizontal ? view.rect.width : view.rect.height;
            _controller.SetMeasuredSize(index, size);
        }

        private void ResizeContent()
        {
            if (_content == null) return;
            var size = _content.sizeDelta;
            if (Horizontal) size.x = _controller.ContentSize;
            else size.y = _controller.ContentSize;
            _content.sizeDelta = size;
        }

        private RectTransform Take(int index)
        {
            RectTransform view;
            if (_pool.Count > 0)
            {
                view = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                view = Instantiate(m_ItemTemplate, _content);
                Hook(view);
            }
            view.gameObject.SetActive(true);
            view.name = m_ItemTemplate.name + " " + index;
            return view;
        }

        private void Release(RectTransform view)
        {
            view.gameObject.SetActive(false);
            _pool.Add(view);
        }

        /// <summary>
        /// Attaches the click/context relay once per created view. The relay looks its index up at
        /// event time rather than capturing it, because a recycled view represents a different item
        /// after every scroll.
        /// </summary>
        private void Hook(RectTransform view)
        {
            var relay = view.gameObject.AddComponent<NXCollectionItemRelay>();
            relay.Owner = this;
        }

        internal int IndexOfView(RectTransform view)
        {
            foreach (var pair in _realized)
                if (pair.Value == view) return pair.Key;
            return -1;
        }

        internal void ReportClick(RectTransform view, bool additive, bool range)
        {
            var index = IndexOfView(view);
            if (index < 0) return;
            _controller.Select(index, additive, range);
            _controller.Activate(index);
        }

        internal void ReportContext(RectTransform view)
        {
            var index = IndexOfView(view);
            if (index >= 0) _controller.RequestContext(index);
        }
    }

    /// <summary>
    /// Per-item event relay added to every realized view by <see cref="NXCollectionView"/>.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class NXCollectionItemRelay : UIBehaviour, IPointerClickHandler
    {
        internal NXCollectionView Owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Owner == null) return;
            var view = (RectTransform)transform;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Owner.ReportContext(view);
                return;
            }

            // Modifiers come through a probe rather than UnityEngine.Input, which throws outright on
            // a project configured for the Input System package alone.
            Owner.ReportClick(view, NXInputModifierProbe.IsAdditive, NXInputModifierProbe.IsRange);
        }
    }
}
