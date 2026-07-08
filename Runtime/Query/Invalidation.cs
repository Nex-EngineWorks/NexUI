using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Query
{
    /// <summary>
    /// Central invalidation bus. Firing a key (or prefix) evicts it from the cache and
    /// notifies any live queries subscribed to that key so they can refetch.
    /// </summary>
    public sealed class Invalidation
    {
        private readonly QueryCache _cache;
        private readonly Dictionary<string, List<Action>> _subscribers =
            new Dictionary<string, List<Action>>();

        public Invalidation(QueryCache cache) => _cache = cache;

        public IDisposable Subscribe(QueryKey key, Action onInvalidated)
        {
            if (onInvalidated == null) return Empty.Instance;
            var full = key.Full;
            if (!_subscribers.TryGetValue(full, out var list))
                _subscribers[full] = list = new List<Action>();
            list.Add(onInvalidated);
            return new Unsub(() => { if (_subscribers.TryGetValue(full, out var l)) l.Remove(onInvalidated); });
        }

        public void Invalidate(QueryKey key)
        {
            _cache?.Invalidate(key);
            if (_subscribers.TryGetValue(key.Full, out var list))
                for (int i = list.Count - 1; i >= 0; i--) list[i]?.Invoke();
        }

        public void InvalidatePrefix(string keyPrefix)
        {
            _cache?.InvalidatePrefix(keyPrefix);
            foreach (var kv in _subscribers)
                if (kv.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                    foreach (var cb in kv.Value) cb?.Invoke();
        }

        private sealed class Unsub : IDisposable
        {
            private Action _a;
            public Unsub(Action a) => _a = a;
            public void Dispose() { _a?.Invoke(); _a = null; }
        }

        private sealed class Empty : IDisposable
        {
            public static readonly Empty Instance = new Empty();
            public void Dispose() { }
        }
    }
}
