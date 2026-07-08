using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a progress bar component.</summary>
    public interface INXProgressBar
    {
        IUIElementHandle Handle { get; }
        float Value { get; set; }
        float Min { get; set; }
        float Max { get; set; }

        /// <summary>Normalized progress in [0, 1].</summary>
        float Normalized { get; }
    }
}
