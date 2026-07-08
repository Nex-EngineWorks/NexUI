using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// Static facade for theme management: holds the registry, the active theme, the
    /// runtime override layer, and the per-backend appliers. Backend-independent; the
    /// only backend touch-point is the registered <see cref="IUIThemeApplier"/>.
    /// </summary>
    public static class NexUIThemeAPI
    {
        public static ThemeRegistry Registry { get; } = new ThemeRegistry();
        public static RuntimeTokenOverride Overrides { get; } = new RuntimeTokenOverride();

        public static UITheme ActiveTheme { get; private set; }

        private static readonly Dictionary<UIRenderBackend, IUIThemeApplier> _appliers =
            new Dictionary<UIRenderBackend, IUIThemeApplier>();

        public static void RegisterTheme(UITheme theme) => Registry.Register(theme);

        public static void RegisterApplier(IUIThemeApplier applier)
        {
            if (applier != null) _appliers[applier.Backend] = applier;
        }

        public static void SetActiveTheme(string themeId)
        {
            if (Registry.TryGet(themeId, out var theme))
            {
                ActiveTheme = theme;
                Overrides.BaseTheme = theme;
            }
            else
            {
                Debug.LogWarning($"[NexUI] SetActiveTheme: theme '{themeId}' not registered.");
            }
        }

        public static string ResolveToken(string key, string fallback = null)
            => Overrides.Resolve(key, fallback ?? ActiveTheme?.Get(key));

        /// <summary>Apply the active theme (with overrides) to a single element.</summary>
        public static void ApplyTo(IUIElementHandle target)
        {
            if (target == null || ActiveTheme == null) return;
            if (!_appliers.TryGetValue(target.Backend, out var applier)) return;

            if (ActiveTheme.tokens != null)
            {
                foreach (var token in ActiveTheme.tokens)
                {
                    if (token == null || string.IsNullOrEmpty(token.key)) continue;
                    var value = Overrides.Resolve(token.key, token.value);
                    applier.ApplyToken(target, token.key, value);
                }
            }
        }
    }
}
