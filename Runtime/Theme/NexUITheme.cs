using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// High-level theme facade (spec API). Wraps <see cref="NexUIThemeAPI"/> and raises
    /// <see cref="ThemeEvents"/> so theme switching and token overrides are observable.
    /// </summary>
    public static class NexUITheme
    {
        public static ThemeRegistry Registry => NexUIThemeAPI.Registry;
        public static UITheme Active => NexUIThemeAPI.ActiveTheme;

        public static void RegisterApplier(IUIThemeApplier applier) => NexUIThemeAPI.RegisterApplier(applier);

        public static void Use(string themeId)
        {
            NexUIThemeAPI.SetActiveTheme(themeId);
            ThemeEvents.RaiseThemeChanged(themeId);
        }

        /// <summary>
        /// Switch theme, optionally flagging a transition. The visual cross-fade itself is a
        /// backend concern; this applies the end-state and notifies listeners.
        /// </summary>
        public static UniTask UseAsync(string themeId, bool transition = false)
        {
            Use(themeId);
            return UniTask.CompletedTask;
        }

        public static void SetToken(string tokenKey, string value)
        {
            NexUIThemeAPI.Overrides.Set(tokenKey, value);
            ThemeEvents.RaiseTokenChanged(tokenKey, value);
        }

        public static string GetToken(string tokenKey) => NexUIThemeAPI.ResolveToken(tokenKey);

        /// <summary>Create a scoped override set applied through the active backend applier.</summary>
        public static ThemeScope CreateScope(IUIThemeApplier applier)
            => new ThemeScope(applier, Active);
    }
}
