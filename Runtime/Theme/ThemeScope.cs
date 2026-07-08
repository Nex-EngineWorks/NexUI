using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// A scoped set of token overrides applied to a specific set of element handles (a
    /// subtree the caller resolves). Disposing the scope re-applies the base theme values
    /// to those elements. Backend-agnostic: works through <see cref="IUIThemeApplier"/>.
    /// </summary>
    public sealed class ThemeScope : IDisposable
    {
        private readonly IUIThemeApplier _applier;
        private readonly List<IUIElementHandle> _targets;
        private readonly RuntimeTokenOverride _overrides;

        public ThemeScope(IUIThemeApplier applier, UITheme baseTheme = null)
        {
            _applier = applier;
            _targets = new List<IUIElementHandle>();
            _overrides = new RuntimeTokenOverride(baseTheme);
        }

        public ThemeScope Add(IUIElementHandle target)
        {
            if (target != null) _targets.Add(target);
            return this;
        }

        public ThemeScope SetToken(string key, string value)
        {
            _overrides.Set(key, value);
            return this;
        }

        /// <summary>Apply the current overrides to every target for the given keys.</summary>
        public void Apply(IEnumerable<string> keys)
        {
            if (_applier == null || keys == null) return;
            foreach (var target in _targets)
                foreach (var key in keys)
                    if (_overrides.TryResolve(key, out var value))
                        _applier.ApplyToken(target, key, value);
        }

        public void Dispose()
        {
            // Re-apply base theme values (if any) to targets, dropping the scoped overrides.
            if (_applier == null || _overrides.BaseTheme == null) return;
            var theme = _overrides.BaseTheme;
            if (theme.tokens == null) return;
            foreach (var target in _targets)
                foreach (var token in theme.tokens)
                    if (token != null && !string.IsNullOrEmpty(token.key))
                        _applier.ApplyToken(target, token.key, token.value);
            _targets.Clear();
        }
    }
}
