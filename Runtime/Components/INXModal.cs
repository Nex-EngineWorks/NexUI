using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a modal dialog component.</summary>
    public interface INXModal
    {
        IUIElementHandle Handle { get; }
        bool IsOpen { get; }

        /// <summary>Raised when the modal requests to close (e.g. backdrop click / close button).</summary>
        event Action<string> CloseRequested;

        void RequestClose(string reason = null);
    }
}
