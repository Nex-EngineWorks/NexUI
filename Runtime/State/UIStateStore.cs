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
        private sealed class Watcher
        {
            public Action<object> Handler;
            public bool RemovedPending;
        }

        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private readonly Dictionary<string, List<Watcher>> _watchers =
            new Dictionary<string, List<Watcher>>();
        /// <summary>>0 while any dispatch runs; removals defer to the end of the outermost one.</summary>
        private int _dispatchDepth;

        public void Set<T>(string key, T value)
        {
            _values[key] = value;
            if (_watchers.TryGetValue(key, out var list))
            {
                // Allocation-free dispatch with snapshot semantics: a watermark excludes watchers
                // added mid-dispatch, and removals defer so a watcher unsubscribing during
                // notification still receives this value (the old ToArray() behaviour).
                _dispatchDepth++;
                var watermark = list.Count;
                try
                {
                    for (int i = 0; i < watermark; i++)
                    {
                        var handler = list[i].Handler;
                        try { handler?.Invoke(value); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                }
                finally
                {
                    _dispatchDepth--;
                    if (_dispatchDepth == 0)
                        foreach (var l in _watchers.Values)
                            l.RemoveAll(IsRemovedPending);
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
                list = new List<Watcher>();
                _watchers[key] = list;
            }
            var node = new Watcher { Handler = Wrapper };
            list.Add(node);

            if (_values.TryGetValue(key, out var current) && current is T currentTyped)
            {
                try { onChanged(currentTyped); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            return new Subscription(() =>
            {
                if (_dispatchDepth > 0)
                {
                    node.RemovedPending = true;
                    return;
                }
                if (_watchers.TryGetValue(key, out var l))
                {
                    l.Remove(node);
                    if (l.Count == 0) _watchers.Remove(key);
                }
            });
        }

        private static bool IsRemovedPending(Watcher node) => node.RemovedPending;

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
