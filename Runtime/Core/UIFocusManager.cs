using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Routes focus trap / release requests to the registered per-backend
    /// <see cref="IUIFocusAdapter"/>. Focus is a backend concern, so Core only
    /// coordinates which adapter handles a given surface.
    /// </summary>
    public sealed class UIFocusManager
    {
        private readonly Dictionary<UIRenderBackend, IUIFocusAdapter> _adapters =
            new Dictionary<UIRenderBackend, IUIFocusAdapter>();

        public void RegisterAdapter(IUIFocusAdapter adapter)
        {
            if (adapter == null) return;
            _adapters[adapter.Backend] = adapter;
        }

        public void Trap(IUISurface surface, string defaultElementId)
        {
            if (surface == null) return;
            if (_adapters.TryGetValue(surface.Backend, out var adapter))
                adapter.Trap(surface, defaultElementId);
        }

        public void Release(IUISurface surface, bool restorePrevious)
        {
            if (surface == null) return;
            if (_adapters.TryGetValue(surface.Backend, out var adapter))
                adapter.Release(surface, restorePrevious);
        }

        public void Clear() => _adapters.Clear();
    }
}
