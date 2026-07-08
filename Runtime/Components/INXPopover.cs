using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a popover anchored to a target element.</summary>
    public interface INXPopover
    {
        IUIElementHandle Handle { get; }
        bool IsOpen { get; }
        event Action Closed;

        void Open(IUIElementHandle anchor);
        void Close();
    }
}
