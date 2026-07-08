using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a (virtualizable) list component.</summary>
    public interface INXList
    {
        IUIElementHandle Handle { get; }
        int Count { get; }
        int SelectedIndex { get; set; }

        event Action<int> SelectionChanged;

        void SetItems(IReadOnlyList<object> items);
        void Refresh();
    }
}
