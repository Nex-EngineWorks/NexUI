using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// Authoring asset holding a named set of design tokens. Well-known keys include:
    /// color.bg, color.surface, color.primary, color.danger, color.text,
    /// space.xs/sm/md/lg, radius.sm/md/lg, motion.fast/normal/slow.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Theme", fileName = "NewTheme")]
    public sealed class UITheme : ScriptableObject
    {
        public string themeId = "default";
        public ThemeToken[] tokens = System.Array.Empty<ThemeToken>();

        private Dictionary<string, string> _lookup;

        private Dictionary<string, string> Lookup
        {
            get
            {
                if (_lookup == null)
                {
                    _lookup = new Dictionary<string, string>();
                    if (tokens != null)
                        foreach (var t in tokens)
                            if (t != null && !string.IsNullOrEmpty(t.key))
                                _lookup[t.key] = t.value;
                }
                return _lookup;
            }
        }

        public bool TryGet(string key, out string value) => Lookup.TryGetValue(key, out value);

        public string Get(string key, string fallback = null)
            => Lookup.TryGetValue(key, out var v) ? v : fallback;

        /// <summary>Call after mutating <see cref="tokens"/> at runtime to rebuild the cache.</summary>
        public void InvalidateCache() => _lookup = null;
    }
}
