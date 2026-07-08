using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Tracks the registered <see cref="IUILayerRoot"/> per layer type per backend and
    /// resolves the correct parent layer surface for a screen being opened.
    /// </summary>
    public sealed class UILayerManager
    {
        // (backend, layer) -> root
        private readonly Dictionary<(UIRenderBackend, UILayerType), IUILayerRoot> _roots =
            new Dictionary<(UIRenderBackend, UILayerType), IUILayerRoot>();

        public void RegisterLayer(IUILayerRoot root)
        {
            if (root == null) return;
            _roots[(root.Backend, root.LayerType)] = root;
        }

        public bool TryGetLayer(UIRenderBackend backend, UILayerType layer, out IUILayerRoot root)
            => _roots.TryGetValue((backend, layer), out root);

        /// <summary>Resolve the parent surface a new screen should be mounted under.</summary>
        public IUISurface ResolveParentSurface(UIRenderBackend backend, UILayerType layer)
        {
            if (_roots.TryGetValue((backend, layer), out var root))
                return root.Surface;

            Debug.LogWarning(
                $"[NexUI] No layer root registered for backend '{backend}' layer '{layer}'. " +
                "Screen will be created without an explicit parent layer.");
            return null;
        }

        public int ResolveBaseSortingOrder(UIRenderBackend backend, UILayerType layer)
        {
            if (_roots.TryGetValue((backend, layer), out var root))
                return root.BaseSortingOrder;
            return (int)layer * 100;
        }

        public void Clear() => _roots.Clear();
    }
}
