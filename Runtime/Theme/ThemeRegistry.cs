using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Theme
{
    /// <summary>Registry of known themes keyed by theme id.</summary>
    public sealed class ThemeRegistry
    {
        private readonly Dictionary<string, UITheme> _themes = new Dictionary<string, UITheme>();

        public IReadOnlyDictionary<string, UITheme> Themes => _themes;

        public void Register(UITheme theme)
        {
            if (theme == null || string.IsNullOrEmpty(theme.themeId))
            {
                Debug.LogWarning("[NexUI] Tried to register a null or unnamed theme.");
                return;
            }
            _themes[theme.themeId] = theme;
        }

        public bool TryGet(string themeId, out UITheme theme) => _themes.TryGetValue(themeId, out theme);

        public UITheme Get(string themeId)
        {
            if (_themes.TryGetValue(themeId, out var theme)) return theme;
            Debug.LogError($"[NexUI] No theme registered with id '{themeId}'.");
            return null;
        }

        public void Clear() => _themes.Clear();
    }
}
