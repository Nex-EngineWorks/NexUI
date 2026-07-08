using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a tooltip component anchored to a target element.</summary>
    public interface INXTooltip
    {
        IUIElementHandle Handle { get; }
        string Text { get; set; }
        bool IsVisible { get; }

        void Show(IUIElementHandle anchor);
        void Hide();
    }
}
