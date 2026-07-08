using System;

namespace emiteat.NexUI.Query
{
    /// <summary>
    /// Identity for a query: a base key plus an optional variant (e.g. a page number or
    /// filter). Two queries with the same <see cref="Full"/> share cache and de-dupe.
    /// </summary>
    public readonly struct QueryKey : IEquatable<QueryKey>
    {
        public string Key { get; }
        public string Variant { get; }

        public QueryKey(string key, string variant = null)
        {
            Key = key ?? string.Empty;
            Variant = variant ?? string.Empty;
        }

        public string Full => string.IsNullOrEmpty(Variant) ? Key : $"{Key}::{Variant}";

        public bool Equals(QueryKey other) => Full == other.Full;
        public override bool Equals(object obj) => obj is QueryKey other && Equals(other);
        public override int GetHashCode() => Full.GetHashCode();
        public override string ToString() => Full;

        public static implicit operator QueryKey(string key) => new QueryKey(key);
    }
}
