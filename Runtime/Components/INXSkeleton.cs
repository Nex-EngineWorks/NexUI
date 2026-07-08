using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Components
{
    /// <summary>Contract for a skeleton loading placeholder.</summary>
    public interface INXSkeleton
    {
        IUIElementHandle Handle { get; }

        /// <summary>When true, shows the shimmering placeholder; when false, reveals content.</summary>
        bool Active { get; set; }
    }
}
