using System;
using System.Collections;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>
    /// Supplies the items a collection shows. Implemented by the game, not by the Designer, so
    /// inventory, quest and shop rules stay out of the UI layer.
    /// </summary>
    public interface INXCollectionSource
    {
        int Count { get; }

        /// <summary>The item at <paramref name="index"/>, or null when the index is out of range.</summary>
        object GetItem(int index);

        /// <summary>Raised when the count or the content changed and views must be rebound.</summary>
        event Action Changed;
    }

    /// <summary>
    /// Adapts any <see cref="IReadOnlyList{T}"/> to <see cref="INXCollectionSource"/>. Call
    /// <see cref="Set"/> or <see cref="Notify"/> when the data behind it changes - the list is not
    /// observed, because observing it would mean copying it.
    /// </summary>
    public sealed class NXCollectionSource<T> : INXCollectionSource
    {
        private IReadOnlyList<T> _items;

        public NXCollectionSource() : this(Array.Empty<T>()) { }

        public NXCollectionSource(IReadOnlyList<T> items) => _items = items ?? Array.Empty<T>();

        public event Action Changed;

        public int Count => _items.Count;

        public IReadOnlyList<T> Items => _items;

        public object GetItem(int index) => index >= 0 && index < _items.Count ? _items[index] : null;

        /// <summary>The typed item at <paramref name="index"/>, saving callers a cast.</summary>
        public T Get(int index) => index >= 0 && index < _items.Count ? _items[index] : default;

        /// <summary>Replaces the backing list and rebinds.</summary>
        public void Set(IReadOnlyList<T> items)
        {
            _items = items ?? Array.Empty<T>();
            Changed?.Invoke();
        }

        /// <summary>Rebinds against the same list, after it was mutated in place.</summary>
        public void Notify() => Changed?.Invoke();
    }

    /// <summary>
    /// The contract every NexUI collection presents, whatever the backend and whatever the preset
    /// (List, Grid, InventoryGrid, Carousel...) is called in the Designer.
    /// </summary>
    /// <remarks>
    /// Presets differ by <see cref="NXCollectionOptions"/> and by their item template - not by a
    /// separate implementation each. That is what makes "an inventory grid" a configuration rather
    /// than a second virtualized list to maintain.
    ///
    /// <see cref="INXList"/> and <see cref="INXGrid"/> remain implemented by the backend adapters so
    /// existing code keeps compiling; they are the narrow view of this same object.
    /// </remarks>
    public interface INXCollectionView
    {
        IUIElementHandle Handle { get; }

        /// <summary>The engine driving layout, selection and state. Subscribe to its events.</summary>
        NXCollectionController Controller { get; }

        /// <summary>Layout, virtualization, selection and interaction settings.</summary>
        NXCollectionOptions Options { get; set; }

        /// <summary>The items on show. Setting it rebinds and resets the scroll position.</summary>
        INXCollectionSource Source { get; set; }

        /// <summary>What the collection is displaying: content, loading, empty or error.</summary>
        NXCollectionState State { get; set; }

        /// <summary>Rebinds the realized views without changing the scroll position.</summary>
        void Refresh();

        /// <summary>Brings <paramref name="index"/> into view.</summary>
        void ScrollTo(int index, NXScrollAlignment alignment = NXScrollAlignment.Nearest);
    }

    /// <summary>
    /// Non-generic item list used by <see cref="INXList.SetItems"/>-style callers. Kept so the
    /// pre-CollectionView API has a migration path that does not need the caller to know about
    /// sources.
    /// </summary>
    public sealed class NXBoxedListSource : INXCollectionSource
    {
        private IReadOnlyList<object> _items = Array.Empty<object>();

        public event Action Changed;

        public int Count => _items.Count;

        public object GetItem(int index) => index >= 0 && index < _items.Count ? _items[index] : null;

        public void Set(IReadOnlyList<object> items)
        {
            _items = items ?? Array.Empty<object>();
            Changed?.Invoke();
        }

        /// <summary>Wraps a non-generic enumerable, for data arriving from serialized or scripted sources.</summary>
        public void Set(IEnumerable items)
        {
            if (items == null) { Set((IReadOnlyList<object>)null); return; }
            var list = new List<object>();
            foreach (var item in items) list.Add(item);
            Set(list);
        }
    }
}
