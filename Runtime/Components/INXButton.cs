using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a button component. Implemented per backend in Integrations.</summary>
    public interface INXButton
    {
        IUIElementHandle Handle { get; }
        event Action Clicked;
        bool Interactable { get; set; }
    }
}
