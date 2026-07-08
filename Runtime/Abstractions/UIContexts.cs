using System;
using System.Collections.Generic;
using System.Threading;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Context passed through the command pipeline. Deliberately backend-agnostic:
    /// it holds a service-lookup delegate and a property bag rather than concrete
    /// runtime types, so Abstractions never depends on Core.
    /// </summary>
    public sealed class UICommandContext
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();

        public CancellationToken CancellationToken { get; }

        /// <summary>Optional service resolver supplied by the host (e.g. UIManager).</summary>
        public Func<Type, object> Services { get; }

        public UICommandContext(Func<Type, object> services = null, CancellationToken cancellationToken = default)
        {
            Services = services;
            CancellationToken = cancellationToken;
        }

        public T Resolve<T>() where T : class
            => Services?.Invoke(typeof(T)) as T;

        public void Set(string key, object value) => _items[key] = value;

        public bool TryGet<T>(string key, out T value)
        {
            if (_items.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Context supplied to screen lifecycle callbacks. References only the
    /// backend-independent <see cref="IUISurface"/> abstraction.
    /// </summary>
    public sealed class UIScreenContext
    {
        public string ScreenId { get; }
        public IUISurface Surface { get; }
        public CancellationToken CancellationToken { get; }

        public UIScreenContext(string screenId, IUISurface surface, CancellationToken cancellationToken = default)
        {
            ScreenId = screenId;
            Surface = surface;
            CancellationToken = cancellationToken;
        }
    }
}
