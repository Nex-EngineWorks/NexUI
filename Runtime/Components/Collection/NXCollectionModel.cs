using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Components
{
    /// <summary>How a collection arranges its items.</summary>
    public enum NXCollectionLayout
    {
        /// <summary>One item per row, scrolling vertically.</summary>
        Vertical,

        /// <summary>One item per column, scrolling horizontally.</summary>
        Horizontal,

        /// <summary>Fixed or derived column count, scrolling vertically.</summary>
        Grid,

        /// <summary>
        /// Column count derived from the viewport width on every layout pass. Items still occupy a
        /// uniform cell: a true variable-width flow needs per-item measurement and is reported as
        /// unsupported by <see cref="NXCollectionOptions.Validate"/> rather than approximated.
        /// </summary>
        Wrap
    }

    /// <summary>How many item views the collection keeps alive.</summary>
    public enum NXVirtualizationMode
    {
        /// <summary>One view per item. Correct for small collections, and the only mode where item size may vary freely.</summary>
        None,

        /// <summary>Views only for the visible window, every item the same size along the scroll axis.</summary>
        FixedSize,

        /// <summary>
        /// Views only for the visible window, item sizes measured as they are realized.
        /// Un-measured items use <see cref="NXCollectionOptions.ItemSize"/> as the estimate, so the
        /// scrollbar settles as the user scrolls rather than being exact from the first frame.
        /// </summary>
        DynamicSize
    }

    /// <summary>How many items may be selected at once.</summary>
    public enum NXSelectionMode
    {
        None,
        Single,
        Multiple
    }

    /// <summary>Interactions a collection accepts. Backends report what they can honour.</summary>
    [Flags]
    public enum NXCollectionInteractions
    {
        None = 0,

        /// <summary>Click, double-click or Submit raises <see cref="NXCollectionController.ItemActivated"/>.</summary>
        Activate = 1 << 0,

        /// <summary>Items can be dragged into a new position inside this collection.</summary>
        Reorder = 1 << 1,

        /// <summary>Items can be dragged to another drop target.</summary>
        DragAndDrop = 1 << 2,

        /// <summary>Right-click or the platform's context button raises a context request.</summary>
        ContextRequest = 1 << 3
    }

    /// <summary>How the collection advances past the visible window.</summary>
    public enum NXPagingMode
    {
        None,

        /// <summary>Raises <see cref="NXCollectionController.LoadMoreRequested"/> near the end.</summary>
        Infinite,

        /// <summary>Fixed-size pages; the source supplies one page at a time.</summary>
        Pagination,

        /// <summary>Scrolling settles on an item boundary.</summary>
        Snap
    }

    /// <summary>
    /// What the collection is showing right now. Shared with StateView so a collection and a
    /// standalone state container speak the same language.
    /// </summary>
    public enum NXCollectionState
    {
        Content,
        Loading,
        Empty,
        Error
    }

    /// <summary>Where an item should land when scrolled into view.</summary>
    public enum NXScrollAlignment
    {
        /// <summary>Scroll the minimum distance that makes the item fully visible.</summary>
        Nearest,
        Start,
        Center,
        End
    }

    /// <summary>A contiguous window of realized item indices.</summary>
    public readonly struct NXCollectionRange : IEquatable<NXCollectionRange>
    {
        public static readonly NXCollectionRange Empty = new NXCollectionRange(0, 0);

        public NXCollectionRange(int firstIndex, int count)
        {
            FirstIndex = firstIndex < 0 ? 0 : firstIndex;
            Count = count < 0 ? 0 : count;
        }

        public int FirstIndex { get; }
        public int Count { get; }
        public int LastIndex => Count == 0 ? FirstIndex - 1 : FirstIndex + Count - 1;
        public bool IsEmpty => Count == 0;

        public bool Contains(int index) => index >= FirstIndex && index <= LastIndex;

        public bool Equals(NXCollectionRange other) => FirstIndex == other.FirstIndex && Count == other.Count;
        public override bool Equals(object obj) => obj is NXCollectionRange other && Equals(other);
        public override int GetHashCode() => (FirstIndex * 397) ^ Count;
        public override string ToString() => IsEmpty ? "[]" : $"[{FirstIndex}..{LastIndex}]";
    }

    /// <summary>
    /// Everything a collection needs to lay itself out, independent of backend. The Designer
    /// serializes these values as component properties and the runtime reads the same fields, so
    /// what is authored is what runs.
    /// </summary>
    public sealed class NXCollectionOptions
    {
        public NXCollectionLayout Layout = NXCollectionLayout.Vertical;
        public NXVirtualizationMode Virtualization = NXVirtualizationMode.FixedSize;
        public NXSelectionMode Selection = NXSelectionMode.Single;
        public NXCollectionInteractions Interactions = NXCollectionInteractions.Activate;
        public NXPagingMode Paging = NXPagingMode.None;

        /// <summary>Item extent along the scroll axis, and the estimate for un-measured items.</summary>
        public float ItemSize = 64f;

        /// <summary>Item extent across the scroll axis. Used by Grid/Wrap to derive columns.</summary>
        public float ItemCrossSize = 64f;

        /// <summary>Gap between items along the scroll axis.</summary>
        public float Spacing = 4f;

        /// <summary>Gap between columns (Grid/Wrap only).</summary>
        public float CrossSpacing = 4f;

        /// <summary>Columns for <see cref="NXCollectionLayout.Grid"/>. Ignored when <see cref="AutoColumns"/> is on.</summary>
        public int ColumnCount = 4;

        /// <summary>Derive the column count from the viewport instead of using <see cref="ColumnCount"/>.</summary>
        public bool AutoColumns;

        /// <summary>Extra rows realized beyond the viewport, to hide pop-in while scrolling fast.</summary>
        public int Overscan = 2;

        /// <summary>Items per page when <see cref="Paging"/> is <see cref="NXPagingMode.Pagination"/>.</summary>
        public int PageSize = 20;

        /// <summary>
        /// How close to the end (in items) infinite paging asks for more. Zero means "at the very
        /// end", which usually shows the user a stall before the next page arrives.
        /// </summary>
        public int LoadMoreThreshold = 5;

        /// <summary>Selecting an item moves focus/scroll to it.</summary>
        public bool ScrollSelectionIntoView = true;

        public NXCollectionOptions Clone() => (NXCollectionOptions)MemberwiseClone();

        /// <summary>
        /// Reports combinations that cannot be honoured as written, so the Designer can surface them
        /// and the runtime can log once instead of behaving unexpectedly. Returns true when the
        /// options are fully supported.
        /// </summary>
        public bool Validate(List<string> problems)
        {
            var ok = true;
            void Problem(string message)
            {
                ok = false;
                problems?.Add(message);
            }

            if (ItemSize <= 0f && Virtualization != NXVirtualizationMode.None)
                Problem("Virtualization needs a positive Item Size to estimate un-measured items.");

            if (Layout == NXCollectionLayout.Wrap && Virtualization == NXVirtualizationMode.DynamicSize)
                Problem("Wrap lays items out in uniform cells, so Dynamic Size cannot be honoured. " +
                        "Use Grid with Dynamic Size, or Wrap with Fixed Size.");

            if (Layout == NXCollectionLayout.Grid && !AutoColumns && ColumnCount < 1)
                Problem("Grid needs at least one column, or Auto Columns turned on.");

            if ((Layout == NXCollectionLayout.Grid || Layout == NXCollectionLayout.Wrap)
                && Virtualization == NXVirtualizationMode.DynamicSize)
                Problem("Grid rows are uniform, so Dynamic Size measures the row, not the item. " +
                        "Item heights inside a row are clamped to the row height.");

            if (Selection == NXSelectionMode.None && ScrollSelectionIntoView)
                Problem("Scroll Selection Into View has no effect while Selection is None.");

            if (Paging == NXPagingMode.Pagination && PageSize < 1)
                Problem("Pagination needs a page size of at least one.");

            if (Paging == NXPagingMode.Snap && Virtualization == NXVirtualizationMode.DynamicSize)
                Problem("Snap needs a predictable item size; Dynamic Size makes snap points move as items measure.");

            return ok;
        }
    }
}
