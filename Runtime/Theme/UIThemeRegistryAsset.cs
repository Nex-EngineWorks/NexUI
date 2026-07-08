using System;
using UnityEngine;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// Central authoring list of themes. Placed in the Theme assembly (not Core) so it can
    /// hold strongly-typed <see cref="UITheme"/> references. Read by validators, the ID
    /// generator, and used to bulk-register themes.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Registry/Theme Registry", fileName = "ThemeRegistry")]
    public sealed class UIThemeRegistryAsset : ScriptableObject
    {
        public UITheme[] themes = Array.Empty<UITheme>();

        public void RegisterAll(ThemeRegistry registry)
        {
            if (registry == null || themes == null) return;
            foreach (var t in themes)
                if (t != null) registry.Register(t);
        }
    }
}
