using System;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// A single design token: a string key (e.g. "color.primary") mapped to a string
    /// value (e.g. "#3B82F6" or "16"). Values are strings so a token can carry colors,
    /// sizes, or durations uniformly; the backend applier interprets them.
    /// </summary>
    [Serializable]
    public sealed class ThemeToken
    {
        public string key;
        public string value;

        public ThemeToken() { }

        public ThemeToken(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }
}
