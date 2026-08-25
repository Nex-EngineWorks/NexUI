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

        /// <summary>
        /// Removes a layer root, but only if it is still the one registered for its slot - so a
        /// destroyed bootstrap never evicts a newer bootstrap's registration.
        /// </summary>
        public bool UnregisterLayer(IUILayerRoot root)
        {
            if (root == null) return false;
            var key = (root.Backend, root.LayerType);
            if (!_roots.TryGetValue(key, out var current) || !ReferenceEquals(current, root)) return false;
            _roots.Remove(key);
            return true;
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
