using System;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Pointer interaction events for gesture-driven motion (hover / press). Optional:
    /// an element handle may or may not provide it depending on backend support.
    /// </summary>
    public interface IUIPointerCapability
    {
        event Action PointerEntered;
        event Action PointerExited;
        event Action PointerDown;
        event Action PointerUp;
    }

    /// <summary>
    /// Focus state events for gesture-driven motion (focus ring, selected state). Optional.
    /// </summary>
    public interface IUIFocusCapability
    {
        event Action Focused;
        event Action Blurred;
        bool HasFocus { get; }
    }
}
