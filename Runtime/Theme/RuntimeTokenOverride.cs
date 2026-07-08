using System.Collections.Generic;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// A layer of token overrides applied on top of a base theme at runtime (e.g. for
    /// accessibility, per-user tweaks, or responsive rules). Resolution checks overrides
    /// first, then falls back to the base theme.
    /// </summary>
    public sealed class RuntimeTokenOverride
    {
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>();

        public UITheme BaseTheme { get; set; }

        public RuntimeTokenOverride(UITheme baseTheme = null) => BaseTheme = baseTheme;

        public void Set(string key, string value) => _overrides[key] = value;
        public void Remove(string key) => _overrides.Remove(key);
        public void ClearOverrides() => _overrides.Clear();

        public bool TryResolve(string key, out string value)
        {
            if (_overrides.TryGetValue(key, out value)) return true;
            if (BaseTheme != null && BaseTheme.TryGet(key, out value)) return true;
            value = null;
            return false;
        }

        public string Resolve(string key, string fallback = null)
            => TryResolve(key, out var v) ? v : fallback;
    }
}
