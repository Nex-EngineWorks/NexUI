using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for an indeterminate loading spinner.</summary>
    public interface INXSpinner
    {
        IUIElementHandle Handle { get; }
        bool Spinning { get; set; }
        float Speed { get; set; }
    }
}
