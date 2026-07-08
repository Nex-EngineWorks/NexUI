using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    public enum NXToastSeverity { Info, Success, Warning, Error }

    /// <summary>Contract for a transient toast notification component.</summary>
    public interface INXToast
    {
        IUIElementHandle Handle { get; }
        string Message { get; set; }
        NXToastSeverity Severity { get; set; }

        /// <summary>Auto-dismiss duration in seconds (0 = manual dismiss).</summary>
        float Duration { get; set; }
    }
}
