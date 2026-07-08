using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a grid component.</summary>
    public interface INXGrid
    {
        IUIElementHandle Handle { get; }
        int Count { get; }
        int ColumnCount { get; set; }
        int SelectedIndex { get; set; }

        event Action<int> SelectionChanged;

        void SetItems(IReadOnlyList<object> items);
        void Refresh();
    }
}
