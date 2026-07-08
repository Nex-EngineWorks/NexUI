using System;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// Global theme event bus. Raised by <see cref="NexUITheme"/> so UI code / integrations
    /// can react to theme switches and token overrides without polling.
    /// </summary>
    public static class ThemeEvents
    {
        /// <summary>Raised with the new theme id when the active theme changes.</summary>
        public static event Action<string> ThemeChanged;

        /// <summary>Raised with (tokenKey, value) when a runtime token override changes.</summary>
        public static event Action<string, string> TokenChanged;

        public static void RaiseThemeChanged(string themeId) => ThemeChanged?.Invoke(themeId);
        public static void RaiseTokenChanged(string key, string value) => TokenChanged?.Invoke(key, value);
    }
}
