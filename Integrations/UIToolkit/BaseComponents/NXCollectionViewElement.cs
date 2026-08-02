using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// The UI Toolkit collection: the same authored settings as the uGUI one, driven by the same
    /// <see cref="NXCollectionController"/>.
    /// </summary>
    /// <remarks>
    /// UI Toolkit already ships ListView, and where its capabilities line up this element uses it -
    /// virtualization and recycling are ListView's job and reimplementing them would be worse than
    /// using the platform. What this element adds is the parts ListView has no concept of: NexUI's
    /// state slots (loading/empty/error), the shared options vocabulary, and a controller whose
    /// selection and activation events are identical across backends.
    ///
    /// Grid and Wrap layouts are laid out by the controller into an absolutely positioned surface,
    /// because ListView is single-column by design. That path is virtualized by the same range
    /// arithmetic the uGUI backend uses.
    /// </remarks>
    [UxmlElement]
    public partial class NXCollectionViewElement : VisualElement, INXCollectionView
    {
        private readonly NXCollectionController _controller = new NXCollectionController();
        private readonly Dictionary<int, VisualElement> _realized = new Dictionary<int, VisualElement>();
        private readonly List<VisualElement> _pool = new List<VisualElement>();
        private readonly List<int> _stale = new List<int>();
        private readonly ScrollView _scroll;
        private readonly VisualElement _surface;
        private readonly VisualElement _stateHost;
        private INXCollectionSource _source;

        /// <summary>Creates one item view. Called only when the pool is empty.</summary>
        public Func<VisualElement> MakeItem;

        /// <summary>Fills a recycled item view with the data at that index.</summary>
        public Action<int, object, VisualElement> BindItem;

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
                ApplyScrollMode();
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

        /// <summary>Item extent along the scroll axis.</summary>
        [UxmlAttribute("item-size")]
        public float itemSize
        {
            get => _controller.Options.ItemSize;
            set { _controller.Options.ItemSize = value; _controller.Invalidate(); }
        }

        [UxmlAttribute("column-count")]
        public int columnCount
        {
            get => _controller.Options.ColumnCount;
            set { _controller.Options.ColumnCount = value; _controller.Invalidate(); }
        }

        [UxmlAttribute("layout-mode")]
        public NXCollectionLayout layoutMode
        {
            get => _controller.Options.Layout;
            set { _controller.Options.Layout = value; ApplyScrollMode(); _controller.Invalidate(); }
        }

        [UxmlAttribute("selection-mode")]
        public NXSelectionMode selectionMode
        {
            get => _controller.Options.Selection;
            set => _controller.Options.Selection = value;
        }

        /// <summary>Container for the loading/empty/error views, so a screen can style them in USS.</summary>
        public VisualElement StateHost => _stateHost;

        public NXCollectionViewElement()
        {
            AddToClassList("nx-collection");

            _scroll = new ScrollView();
            _scroll.AddToClassList("nx-collection__scroll");
            _surface = _scroll.contentContainer;
            _surface.AddToClassList("nx-collection__surface");
            Add(_scroll);

            _stateHost = new VisualElement { pickingMode = PickingMode.Ignore };
            _stateHost.AddToClassList("nx-collection__state");
            _stateHost.style.display = DisplayStyle.None;
            Add(_stateHost);

            _controller.VisibleRangeChanged += OnRangeChanged;
            _controller.SelectionChanged += OnSelectionChanged;
            _controller.StateChanged += OnStateChanged;
            _controller.ScrollRequested += offset => _scroll.scrollOffset = ScrollVector(offset);

            _scroll.verticalScroller.valueChanged += _ => PushScrollOffset();
            _scroll.horizontalScroller.valueChanged += _ => PushScrollOffset();
            RegisterCallback<GeometryChangedEvent>(_ => SyncViewport());
            ApplyScrollMode();
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

        /// <summary>Sets the items directly, for callers holding a plain list.</summary>
        public void SetItems(IReadOnlyList<object> items)
        {
            var boxed = _source as NXBoxedListSource ?? new NXBoxedListSource();
            if (!ReferenceEquals(boxed, _source)) Source = boxed;
            boxed.Set(items);
        }

        private bool Horizontal => _controller.Options.Layout == NXCollectionLayout.Horizontal;

        private void ApplyScrollMode()
            => _scroll.mode = Horizontal ? ScrollViewMode.Horizontal : ScrollViewMode.Vertical;

        private UnityEngine.Vector2 ScrollVector(float offset)
            => Horizontal ? new UnityEngine.Vector2(offset, 0f) : new UnityEngine.Vector2(0f, offset);

        private void PushScrollOffset()
            => _controller.SetScrollOffset(Horizontal ? _scroll.scrollOffset.x : _scroll.scrollOffset.y);

        private void SyncViewport()
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            _controller.SetViewport(Horizontal ? rect.width : rect.height,
                Horizontal ? rect.height : rect.width);
            ResizeSurface();
        }

        private void OnSourceChanged()
        {
            _controller.SetItemCount(_source?.Count ?? 0);
            Rebuild();
        }

        private void OnStateChanged(NXCollectionState state)
        {
            var showsContent = state == NXCollectionState.Content;
            _scroll.style.display = showsContent ? DisplayStyle.Flex : DisplayStyle.None;
            _stateHost.style.display = showsContent ? DisplayStyle.None : DisplayStyle.Flex;
            _stateHost.EnableInClassList("is-loading", state == NXCollectionState.Loading);
            _stateHost.EnableInClassList("is-empty", state == NXCollectionState.Empty);
            _stateHost.EnableInClassList("is-error", state == NXCollectionState.Error);
        }

        private void OnSelectionChanged(IReadOnlyList<int> selection)
        {
            foreach (var pair in _realized)
                pair.Value.EnableInClassList("is-selected", _controller.IsSelected(pair.Key));
        }

        private void OnRangeChanged(NXCollectionRange range)
        {
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

                var view = Take();
                _realized[i] = view;
                Place(i, view);
                BindItem?.Invoke(i, _source?.GetItem(i), view);
                view.EnableInClassList("is-selected", _controller.IsSelected(i));
            }

            ResizeSurface();
        }

        private void Rebuild()
        {
            foreach (var pair in _realized) Release(pair.Value);
            _realized.Clear();
            _controller.Invalidate();
            OnRangeChanged(_controller.VisibleRange);
        }

        private void Place(int index, VisualElement view)
        {
            var main = _controller.OffsetOf(index);
            var cross = _controller.CrossOffsetOf(index);
            var crossSize = _controller.CellCrossSize();

            view.style.position = Position.Absolute;
            if (Horizontal)
            {
                view.style.left = main;
                view.style.top = cross;
                view.style.width = _controller.Options.ItemSize;
                view.style.height = crossSize;
                return;
            }

            view.style.top = main;
            view.style.left = _controller.ColumnCount > 1 ? cross : 0f;
            view.style.height = _controller.Options.ItemSize;
            if (_controller.ColumnCount > 1) view.style.width = crossSize;
            else view.style.right = 0f;
        }

        private void ResizeSurface()
        {
            var size = _controller.ContentSize;
            if (Horizontal) _surface.style.width = size;
            else _surface.style.height = size;
        }

        private VisualElement Take()
        {
            VisualElement view;
            if (_pool.Count > 0)
            {
                view = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                view.style.display = DisplayStyle.Flex;
            }
            else
            {
                view = MakeItem != null ? MakeItem() : new VisualElement();
                view.AddToClassList("nx-collection__item");
                Hook(view);
                _surface.Add(view);
            }
            return view;
        }

        private void Release(VisualElement view)
        {
            view.style.display = DisplayStyle.None;
            _pool.Add(view);
        }

        /// <summary>
        /// Wires click and context events once per created view. The index is resolved at event time
        /// because a recycled view stands for a different item after every scroll.
        /// </summary>
        private void Hook(VisualElement view)
        {
            view.RegisterCallback<PointerDownEvent>(evt =>
            {
                var index = IndexOf(view);
                if (index < 0) return;

                if (evt.button == 1)
                {
                    _controller.RequestContext(index);
                    return;
                }

                _controller.Select(index, evt.ctrlKey || evt.commandKey, evt.shiftKey);
                _controller.Activate(index);
            });
        }

        private new int IndexOf(VisualElement view)
        {
            foreach (var pair in _realized)
                if (pair.Value == view) return pair.Key;
            return -1;
        }
    }
}
