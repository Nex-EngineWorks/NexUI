using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Core.Registry
{
    /// <summary>
    /// Named reusable template lookup. Templates are stored as <see cref="UnityEngine.Object"/>
    /// (VisualTreeAsset for UI Toolkit, prefab for uGUI) so Core stays backend-agnostic; the
    /// Integration casts to the concrete type.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Registry/Template Registry", fileName = "TemplateRegistry")]
    public sealed class UITemplateRegistryAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public UnityEngine.Object template;
            public Abstractions.UIRenderBackend backend;
        }

        public Entry[] templates = Array.Empty<Entry>();

        private Dictionary<string, Entry> _lookup;

        public bool TryGet(string key, out Entry entry)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Entry>();
                foreach (var e in templates)
                    if (!string.IsNullOrEmpty(e.key)) _lookup[e.key] = e;
            }
            return _lookup.TryGetValue(key, out entry);
        }

        public void InvalidateCache() => _lookup = null;
    }
}
