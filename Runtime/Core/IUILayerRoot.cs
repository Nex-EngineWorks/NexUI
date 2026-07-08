using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Backend-independent parent container for one <see cref="UILayerType"/>.
    /// The Integration provides an implementation whose <see cref="Surface"/> is
    /// passed to the screen factory as the parent layer for new screens.
    /// </summary>
    public interface IUILayerRoot
    {
        UILayerType LayerType { get; }
        UIRenderBackend Backend { get; }

        /// <summary>Surface acting as the mount point for screens on this layer.</summary>
        IUISurface Surface { get; }

        /// <summary>Base sorting order for this layer; screens offset from here.</summary>
        int BaseSortingOrder { get; }
    }
}
