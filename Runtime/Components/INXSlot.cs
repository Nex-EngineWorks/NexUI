using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>
    /// Contract for a content slot: a named mount point that hosts arbitrary child
    /// content (composition primitive for building compound components).
    /// </summary>
    public interface INXSlot
    {
        IUIElementHandle Handle { get; }
        string SlotName { get; }
        bool HasContent { get; }

        void SetContent(IUIElementHandle content);
        void Clear();
    }
}
