using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// Simple observable key/value store. Values are boxed as objects; typed access
    /// is provided through generic methods. Watchers are notified on every Set.
    /// </summary>
    public sealed class UIStateStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private readonly Dictionary<string, List<Action<object>>> _watchers =
            new Dictionary<string, List<Action<object>>>();

        public void Set<T>(string key, T value)
        {
            _values[key] = value;
            if (_watchers.TryGetValue(key, out var list))
            {
                // Iterate a copy so a watcher may unsubscribe during notification.
                var snapshot = list.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    try { snapshot[i]?.Invoke(value); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
        }

        public T Get<T>(string key)
            => TryGet<T>(key, out var v) ? v : default;

        public bool TryGet<T>(string key, out T value)
        {
            if (_values.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public bool Contains(string key) => _values.ContainsKey(key);

        /// <summary>All keys currently held (for debug inspection).</summary>
        public IReadOnlyCollection<string> Keys => _values.Keys;

        /// <summary>
        /// Watch a key for changes. Fires immediately with the current value if present.
        /// Dispose the returned handle to stop watching.
        /// </summary>
        public IDisposable Watch<T>(string key, Action<T> onChanged)
        {
            if (onChanged == null) return Subscription.Empty;

            void Wrapper(object boxed)
            {
                if (boxed is T typed) onChanged(typed);
            }

            if (!_watchers.TryGetValue(key, out var list))
            {
                list = new List<Action<object>>();
                _watchers[key] = list;
            }
            list.Add(Wrapper);

            if (_values.TryGetValue(key, out var current) && current is T currentTyped)
            {
                try { onChanged(currentTyped); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            return new Subscription(() =>
            {
                if (_watchers.TryGetValue(key, out var l))
                {
                    l.Remove(Wrapper);
                    if (l.Count == 0) _watchers.Remove(key);
                }
            });
        }

        public void Clear()
        {
            _values.Clear();
            _watchers.Clear();
        }

        private sealed class Subscription : IDisposable
        {
            public static readonly Subscription Empty = new Subscription(null);
            private Action _dispose;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
