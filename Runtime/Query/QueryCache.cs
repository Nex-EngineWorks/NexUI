using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Query
{
    /// <summary>
    /// In-memory cache of query results with a staleness window. Backend-independent and
    /// free of any Core dependency; keyed by <see cref="QueryKey"/>.
    /// </summary>
    public sealed class QueryCache
    {
        private struct Entry
        {
            public object Value;
            public DateTime Timestamp;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(30);

        public bool TryGet<T>(QueryKey key, out T value, out bool isStale)
        {
            if (_entries.TryGetValue(key.Full, out var entry) && entry.Value is T typed)
            {
                value = typed;
                isStale = DateTime.UtcNow - entry.Timestamp > StaleAfter;
                return true;
            }
            value = default;
            isStale = true;
            return false;
        }

        public void Set<T>(QueryKey key, T value)
            => _entries[key.Full] = new Entry { Value = value, Timestamp = DateTime.UtcNow };

        public void Invalidate(QueryKey key) => _entries.Remove(key.Full);

        public void InvalidatePrefix(string keyPrefix)
        {
            var toRemove = new List<string>();
            foreach (var kv in _entries)
                if (kv.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                    toRemove.Add(kv.Key);
            foreach (var k in toRemove) _entries.Remove(k);
        }

        public int Count => _entries.Count;

        public void Clear() => _entries.Clear();
    }
}
