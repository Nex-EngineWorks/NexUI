using System;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// A named component variant: a set of token overrides applied to a specific component
    /// role (e.g. "Button/primary", "Button/danger"). Purely data; the applier resolves
    /// the tokens onto elements.
    /// </summary>
    [Serializable]
    public sealed class ThemeVariant
    {
        public string component;
        public string variant;
        public ThemeToken[] tokens = Array.Empty<ThemeToken>();

        public string Key => $"{component}/{variant}";

        public bool TryGet(string tokenKey, out string value)
        {
            if (tokens != null)
                foreach (var t in tokens)
                    if (t != null && t.key == tokenKey) { value = t.value; return true; }
            value = null;
            return false;
        }
    }
}
