using UnityEngine;

namespace emiteat.NexUI.Settings
{
    /// <summary>
    /// Locates and caches the active <see cref="NexUISettings"/>. Resolution order:
    /// an explicitly assigned instance, then <c>Resources/NexUISettings</c>.
    /// </summary>
    public static class NexUISettingsProvider
    {
        public const string ResourcesPath = "NexUISettings";

        private static NexUISettings _cached;
        private static bool _resolved;

        /// <summary>Explicitly set the active settings (e.g. from a DI container).</summary>
        public static void Set(NexUISettings settings)
        {
            _cached = settings;
            _resolved = true;
        }

        public static NexUISettings Current
        {
            get
            {
                if (!_resolved)
                {
                    _cached = Resources.Load<NexUISettings>(ResourcesPath);
                    _resolved = true;
                }
                return _cached;
            }
        }

        public static bool HasSettings => Current != null;

        public static void Reset()
        {
            _cached = null;
            _resolved = false;
        }
    }
}
