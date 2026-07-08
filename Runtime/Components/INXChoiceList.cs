using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a single/multi choice list (radio / checkbox group).</summary>
    public interface INXChoiceList
    {
        IUIElementHandle Handle { get; }
        bool AllowMultiple { get; set; }

        IReadOnlyList<string> Options { get; }
        IReadOnlyList<int> SelectedIndices { get; }

        event Action<IReadOnlyList<int>> SelectionChanged;

        void SetOptions(IReadOnlyList<string> options);
        void Select(int index, bool selected);
    }
}
