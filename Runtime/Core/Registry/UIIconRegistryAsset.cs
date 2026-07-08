using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Core.Registry
{
    /// <summary>
    /// Named icon lookup (backend-agnostic: stores <see cref="Sprite"/> which both uGUI and
    /// UI Toolkit can use). Consumed by components and validated for duplicate/missing keys.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Registry/Icon Registry", fileName = "IconRegistry")]
    public sealed class UIIconRegistryAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public Sprite sprite;
        }

        public Entry[] icons = Array.Empty<Entry>();

        private Dictionary<string, Sprite> _lookup;

        public bool TryGet(string key, out Sprite sprite)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Sprite>();
                foreach (var e in icons)
                    if (!string.IsNullOrEmpty(e.key)) _lookup[e.key] = e.sprite;
            }
            return _lookup.TryGetValue(key, out sprite);
        }

        public void InvalidateCache() => _lookup = null;
    }
}
