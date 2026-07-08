using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// Applies a theme's tokens to a set of element handles through a backend
    /// <see cref="IUIThemeApplier"/>. A full animated cross-fade between themes is a
    /// backend concern; this applies the resolved end-state token values.
    /// </summary>
    public sealed class ThemeTransition
    {
        private readonly IUIThemeApplier _applier;

        public ThemeTransition(IUIThemeApplier applier) => _applier = applier;

        /// <summary>Apply every token of <paramref name="theme"/> to <paramref name="target"/>.</summary>
        public void Apply(IUIElementHandle target, UITheme theme)
        {
            if (_applier == null || target == null || theme == null || theme.tokens == null) return;
            foreach (var token in theme.tokens)
                if (token != null && !string.IsNullOrEmpty(token.key))
                    _applier.ApplyToken(target, token.key, token.value);
        }

        /// <summary>Apply a resolved override layer (overrides + base theme) to a target.</summary>
        public void Apply(IUIElementHandle target, RuntimeTokenOverride overrides, IEnumerable<string> keys)
        {
            if (_applier == null || target == null || overrides == null || keys == null) return;
            foreach (var key in keys)
                if (overrides.TryResolve(key, out var value))
                    _applier.ApplyToken(target, key, value);
        }
    }
}
