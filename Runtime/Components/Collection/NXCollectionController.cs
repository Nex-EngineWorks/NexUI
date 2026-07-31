using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Components
{
    /// <summary>
    /// The backend-independent engine behind every NexUI collection: it owns the item count, the
    /// selection, the display state and the arithmetic that turns a scroll offset into "which items
    /// are on screen and where".
    /// </summary>
    /// <remarks>
    /// Deliberately free of UnityEngine types. A uGUI ScrollRect and a UI Toolkit ScrollView differ
    /// in how they report a viewport and how they place a view, not in what a virtualized list has
    /// to compute - so that computation lives here once and is unit-testable without a scene.
    ///
    /// The controller never owns item data. A source supplies the count and binds a view to an
    /// index (<see cref="INXCollectionView"/>), which is what keeps inventory rules, quest rules and
    /// shop rules out of the collection.
    ///
    /// Coordinates are one-dimensional along the scroll axis, with 0 at the start of the content.
    /// The backend maps that onto its own axis direction.
    /// </remarks>
    public sealed class NXCollectionController
    {
        private NXCollectionOptions _options = new NXCollectionOptions();
        private readonly List<int> _selection = new List<int>();
        private float[] _measured = Array.Empty<float>();
        private float[] _offsets = Array.Empty<float>();
        private bool _offsetsDirty = true;
        private int _itemCount;
        private int _columns = 1;
        private float _viewportMain;
        private float _viewportCross;
        private float _scrollOffset;
        private int _selectionAnchor = -1;
        private NXCollectionState _state = NXCollectionState.Content;
        private NXCollectionRange _range = NXCollectionRange.Empty;
        private bool _loadMoreRaisedForCount = false;

        /// <summary>Raised when the realized window changes; the backend rebinds views in response.</summary>
        public event Action<NXCollectionRange> VisibleRangeChanged;

        /// <summary>Raised whenever the selected set changes, including when it is cleared.</summary>
        public event Action<IReadOnlyList<int>> SelectionChanged;

        /// <summary>Raised when an item is activated (click, double-click or Submit).</summary>
        public event Action<int> ItemActivated;

        /// <summary>Raised when a context menu is requested on an item.</summary>
        public event Action<int> ContextRequested;

        /// <summary>Raised when the user reorders an item. The data source performs the actual move.</summary>
        public event Action<int, int> ItemMoved;

        /// <summary>Raised when the display state changes.</summary>
        public event Action<NXCollectionState> StateChanged;

        /// <summary>Raised once per count while infinite paging is near the end of the content.</summary>
        public event Action LoadMoreRequested;

        /// <summary>Raised when the controller wants the backend to scroll to an offset.</summary>
        public event Action<float> ScrollRequested;

        public NXCollectionOptions Options
        {
            get => _options;
            set
            {
                _options = value ?? new NXCollectionOptions();
                Invalidate();
            }
        }

        public int ItemCount => _itemCount;

        /// <summary>Columns currently in use. Always 1 for Vertical/Horizontal.</summary>
        public int ColumnCount => _columns;

        /// <summary>Rows (or columns, when horizontal) the content is made of.</summary>
        public int LineCount => _columns <= 0 ? 0 : (_itemCount + _columns - 1) / _columns;

        public NXCollectionRange VisibleRange => _range;

        public float ScrollOffset => _scrollOffset;

        /// <summary>Total extent of the content along the scroll axis.</summary>
        public float ContentSize
        {
            get
            {
                EnsureOffsets();
                var lines = LineCount;
                if (lines == 0) return 0f;
                return _offsets[lines - 1] + LineSize(lines - 1);
            }
        }

        /// <summary>
        /// What the collection is showing. Setting <see cref="NXCollectionState.Content"/> with no
        /// items resolves to <see cref="NXCollectionState.Empty"/>, so a caller never has to remember
        /// to special-case the empty list.
        /// </summary>
        public NXCollectionState State
        {
            get => _state;
            set
            {
                var resolved = value == NXCollectionState.Content && _itemCount == 0
                    ? NXCollectionState.Empty
                    : value;
                if (_state == resolved) return;
                _state = resolved;
                StateChanged?.Invoke(resolved);
            }
        }

        public int SelectedIndex => _selection.Count > 0 ? _selection[_selection.Count - 1] : -1;

        public IReadOnlyList<int> SelectedIndices => _selection;

        // ---- Data ---------------------------------------------------------------------------

        /// <summary>
        /// Sets how many items exist. Selection is pruned to the new count and the state is
        /// re-resolved, so shrinking a list can never leave a selection pointing past the end.
        /// </summary>
        public void SetItemCount(int count)
        {
            if (count < 0) count = 0;
            if (_itemCount == count) return;
            _itemCount = count;

            if (_measured.Length < count) Array.Resize(ref _measured, count);
            for (var i = 0; i < _measured.Length; i++)
                if (i >= count) _measured[i] = 0f;

            _loadMoreRaisedForCount = false;
            PruneSelection();
            Invalidate();

            if (_state == NXCollectionState.Content && count == 0) State = NXCollectionState.Content;
            else if (_state == NXCollectionState.Empty && count > 0) State = NXCollectionState.Content;
        }

        /// <summary>
        /// Records the measured extent of a realized item. Only meaningful under
        /// <see cref="NXVirtualizationMode.DynamicSize"/>; ignored otherwise so a backend can call it
        /// unconditionally.
        /// </summary>
        public void SetMeasuredSize(int index, float size)
        {
            if (_options.Virtualization != NXVirtualizationMode.DynamicSize) return;
            if (index < 0 || index >= _itemCount || size <= 0f) return;
            if (_measured.Length <= index) Array.Resize(ref _measured, _itemCount);
            if (Math.Abs(_measured[index] - size) < 0.01f) return;
            _measured[index] = size;
            _offsetsDirty = true;
        }

        // ---- Viewport & scrolling -----------------------------------------------------------

        /// <summary>Reports the viewport size. Cross size drives the column count for Grid/Wrap.</summary>
        public void SetViewport(float mainAxisSize, float crossAxisSize)
        {
            if (Nearly(_viewportMain, mainAxisSize) && Nearly(_viewportCross, crossAxisSize)) return;
            _viewportMain = Math.Max(0f, mainAxisSize);
            _viewportCross = Math.Max(0f, crossAxisSize);
            Invalidate();
        }

        /// <summary>Reports the current scroll offset and re-resolves the realized window.</summary>
        public void SetScrollOffset(float offset)
        {
            if (offset < 0f) offset = 0f;
            if (Nearly(_scrollOffset, offset)) return;
            _scrollOffset = offset;
            ResolveRange();
            RequestMoreIfNeeded();
        }

        /// <summary>Offset at which <paramref name="index"/> starts, along the scroll axis.</summary>
        public float OffsetOf(int index)
        {
            if (index <= 0 || _columns <= 0) return 0f;
            EnsureOffsets();
            var line = index / _columns;
            return line < _offsets.Length ? _offsets[line] : 0f;
        }

        /// <summary>Column of <paramref name="index"/>. Always 0 for Vertical/Horizontal.</summary>
        public int ColumnOf(int index) => _columns <= 1 ? 0 : index % _columns;

        /// <summary>Cross-axis offset of <paramref name="index"/>, for Grid/Wrap placement.</summary>
        public float CrossOffsetOf(int index)
            => _columns <= 1 ? 0f : ColumnOf(index) * (CellCrossSize() + _options.CrossSpacing);

        /// <summary>Cross-axis extent one item occupies.</summary>
        public float CellCrossSize()
        {
            if (_columns <= 1) return _viewportCross;
            var available = _viewportCross - _options.CrossSpacing * (_columns - 1);
            return available <= 0f ? _options.ItemCrossSize : available / _columns;
        }

        /// <summary>
        /// Computes the scroll offset that brings <paramref name="index"/> into view and asks the
        /// backend to move there. No-op when the item is already fully visible and the alignment is
        /// <see cref="NXScrollAlignment.Nearest"/>.
        /// </summary>
        public void ScrollTo(int index, NXScrollAlignment alignment = NXScrollAlignment.Nearest)
        {
            if (index < 0 || index >= _itemCount) return;
            EnsureOffsets();

            var start = OffsetOf(index);
            var size = LineSize(index / Math.Max(1, _columns));
            var end = start + size;
            var maxOffset = Math.Max(0f, ContentSize - _viewportMain);
            float target;

            switch (alignment)
            {
                case NXScrollAlignment.Start: target = start; break;
                case NXScrollAlignment.Center: target = start - (_viewportMain - size) * 0.5f; break;
                case NXScrollAlignment.End: target = end - _viewportMain; break;
                default:
                    if (start >= _scrollOffset && end <= _scrollOffset + _viewportMain) return;
                    target = start < _scrollOffset ? start : end - _viewportMain;
                    break;
            }

            target = Clamp(target, 0f, maxOffset);
            if (Nearly(target, _scrollOffset)) return;
            _scrollOffset = target;
            ResolveRange();
            ScrollRequested?.Invoke(target);
        }

        /// <summary>Nearest item boundary to the current offset, for <see cref="NXPagingMode.Snap"/>.</summary>
        public int SnapIndex()
        {
            if (_itemCount == 0 || _columns <= 0) return -1;
            EnsureOffsets();
            var lines = LineCount;
            var best = 0;
            var bestDistance = float.MaxValue;
            for (var line = 0; line < lines; line++)
            {
                var distance = Math.Abs(_offsets[line] - _scrollOffset);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = line;
            }
            return Math.Min(_itemCount - 1, best * _columns);
        }

        // ---- Selection ----------------------------------------------------------------------

        /// <summary>
        /// Selects <paramref name="index"/>. <paramref name="additive"/> toggles it into a multiple
        /// selection (Ctrl-click); <paramref name="rangeFromAnchor"/> extends from the last plain
        /// selection (Shift-click). Both are ignored unless the mode is
        /// <see cref="NXSelectionMode.Multiple"/>.
        /// </summary>
        public void Select(int index, bool additive = false, bool rangeFromAnchor = false)
        {
            if (_options.Selection == NXSelectionMode.None) return;
            if (index < 0 || index >= _itemCount) return;

            var multiple = _options.Selection == NXSelectionMode.Multiple;
            var changed = false;

            if (multiple && rangeFromAnchor && _selectionAnchor >= 0)
            {
                var from = Math.Min(_selectionAnchor, index);
                var to = Math.Max(_selectionAnchor, index);
                _selection.Clear();
                for (var i = from; i <= to; i++) _selection.Add(i);
                changed = true;
            }
            else if (multiple && additive)
            {
                if (_selection.Remove(index)) changed = true;
                else { _selection.Add(index); changed = true; }
                _selectionAnchor = index;
            }
            else
            {
                if (_selection.Count != 1 || _selection[0] != index)
                {
                    _selection.Clear();
                    _selection.Add(index);
                    changed = true;
                }
                _selectionAnchor = index;
            }

            if (!changed) return;
            SelectionChanged?.Invoke(_selection);
            if (_options.ScrollSelectionIntoView && _selection.Count > 0)
                ScrollTo(index);
        }

        public bool IsSelected(int index) => _selection.Contains(index);

        public void ClearSelection()
        {
            if (_selection.Count == 0) return;
            _selection.Clear();
            _selectionAnchor = -1;
            SelectionChanged?.Invoke(_selection);
        }

        /// <summary>
        /// Moves the selection by <paramref name="delta"/> items - what a directional pad press does.
        /// Grid navigation passes ±<see cref="ColumnCount"/> for vertical moves.
        /// </summary>
        public void MoveSelection(int delta)
        {
            if (_itemCount == 0 || _options.Selection == NXSelectionMode.None) return;
            var current = SelectedIndex;
            var next = current < 0 ? 0 : Clamp(current + delta, 0, _itemCount - 1);
            Select(next);
        }

        // ---- Interactions -------------------------------------------------------------------

        /// <summary>Raises <see cref="ItemActivated"/> when the options allow activation.</summary>
        public void Activate(int index)
        {
            if ((_options.Interactions & NXCollectionInteractions.Activate) == 0) return;
            if (index < 0 || index >= _itemCount) return;
            ItemActivated?.Invoke(index);
        }

        /// <summary>Raises <see cref="ContextRequested"/> when the options allow it.</summary>
        public void RequestContext(int index)
        {
            if ((_options.Interactions & NXCollectionInteractions.ContextRequest) == 0) return;
            if (index < 0 || index >= _itemCount) return;
            ContextRequested?.Invoke(index);
        }

        /// <summary>
        /// Reports a reorder. The controller does not move data - it re-points the selection and
        /// raises <see cref="ItemMoved"/> so the source can perform the move it authorises.
        /// </summary>
        public bool Move(int fromIndex, int toIndex)
        {
            if ((_options.Interactions & NXCollectionInteractions.Reorder) == 0) return false;
            if (fromIndex < 0 || fromIndex >= _itemCount) return false;
            if (toIndex < 0 || toIndex >= _itemCount || toIndex == fromIndex) return false;

            for (var i = 0; i < _selection.Count; i++)
            {
                if (_selection[i] == fromIndex) _selection[i] = toIndex;
                else if (fromIndex < toIndex && _selection[i] > fromIndex && _selection[i] <= toIndex) _selection[i]--;
                else if (fromIndex > toIndex && _selection[i] >= toIndex && _selection[i] < fromIndex) _selection[i]++;
            }

            ItemMoved?.Invoke(fromIndex, toIndex);
            return true;
        }

        // ---- Layout arithmetic ---------------------------------------------------------------

        /// <summary>Recomputes columns and the realized window. Call after any layout input changes.</summary>
        public void Invalidate()
        {
            _columns = ResolveColumns();
            _offsetsDirty = true;
            ResolveRange();
        }

        private int ResolveColumns()
        {
            switch (_options.Layout)
            {
                case NXCollectionLayout.Grid when !_options.AutoColumns:
                    return Math.Max(1, _options.ColumnCount);
                case NXCollectionLayout.Grid:
                case NXCollectionLayout.Wrap:
                {
                    var cell = _options.ItemCrossSize + _options.CrossSpacing;
                    if (cell <= 0f || _viewportCross <= 0f) return Math.Max(1, _options.ColumnCount);
                    return Math.Max(1, (int)((_viewportCross + _options.CrossSpacing) / cell));
                }
                default:
                    return 1;
            }
        }

        private float LineSize(int line)
        {
            if (_options.Virtualization != NXVirtualizationMode.DynamicSize || _columns <= 0)
                return _options.ItemSize;

            // A line is as tall as its tallest measured item; un-measured items fall back to the
            // configured estimate, which is what lets the scrollbar exist before anything is realized.
            var size = 0f;
            var first = line * _columns;
            for (var i = first; i < first + _columns && i < _itemCount; i++)
            {
                var measured = i < _measured.Length && _measured[i] > 0f ? _measured[i] : _options.ItemSize;
                if (measured > size) size = measured;
            }
            return size <= 0f ? _options.ItemSize : size;
        }

        private void EnsureOffsets()
        {
            if (!_offsetsDirty) return;
            _offsetsDirty = false;

            var lines = LineCount;
            if (_offsets.Length < lines) Array.Resize(ref _offsets, Math.Max(lines, 8));

            var cursor = 0f;
            for (var line = 0; line < lines; line++)
            {
                _offsets[line] = cursor;
                cursor += LineSize(line) + _options.Spacing;
            }
        }

        private void ResolveRange()
        {
            var previous = _range;
            _range = ComputeRange();
            if (!_range.Equals(previous)) VisibleRangeChanged?.Invoke(_range);
        }

        private NXCollectionRange ComputeRange()
        {
            if (_itemCount == 0 || _columns <= 0) return NXCollectionRange.Empty;

            if (_options.Virtualization == NXVirtualizationMode.None)
                return new NXCollectionRange(0, _itemCount);

            EnsureOffsets();
            var lines = LineCount;
            if (_viewportMain <= 0f)
                // Before the first layout pass the viewport is unknown; realizing the overscan window
                // keeps the first frame from flashing empty.
                return new NXCollectionRange(0, Math.Min(_itemCount, Math.Max(1, _options.Overscan) * _columns));

            var firstLine = 0;
            for (var line = 0; line < lines; line++)
            {
                if (_offsets[line] + LineSize(line) > _scrollOffset) { firstLine = line; break; }
                firstLine = line;
            }

            var lastLine = firstLine;
            var limit = _scrollOffset + _viewportMain;
            for (var line = firstLine; line < lines; line++)
            {
                lastLine = line;
                if (_offsets[line] >= limit) break;
            }

            firstLine = Math.Max(0, firstLine - _options.Overscan);
            lastLine = Math.Min(lines - 1, lastLine + _options.Overscan);

            var first = firstLine * _columns;
            var count = Math.Min(_itemCount - first, (lastLine - firstLine + 1) * _columns);
            return new NXCollectionRange(first, count);
        }

        private void RequestMoreIfNeeded()
        {
            if (_options.Paging != NXPagingMode.Infinite || _itemCount == 0) return;
            if (_loadMoreRaisedForCount) return;
            if (_range.LastIndex < _itemCount - 1 - _options.LoadMoreThreshold) return;

            // Once per count: the next SetItemCount re-arms it, so scrolling back and forth at the
            // end does not fire a request per frame.
            _loadMoreRaisedForCount = true;
            LoadMoreRequested?.Invoke();
        }

        private void PruneSelection()
        {
            var removed = false;
            for (var i = _selection.Count - 1; i >= 0; i--)
                if (_selection[i] >= _itemCount) { _selection.RemoveAt(i); removed = true; }
            if (_selectionAnchor >= _itemCount) _selectionAnchor = -1;
            if (removed) SelectionChanged?.Invoke(_selection);
        }

        private static bool Nearly(float a, float b) => Math.Abs(a - b) < 0.01f;

        private static float Clamp(float value, float min, float max)
            => value < min ? min : value > max ? max : value;

        private static int Clamp(int value, int min, int max)
            => value < min ? min : value > max ? max : value;
    }
}
