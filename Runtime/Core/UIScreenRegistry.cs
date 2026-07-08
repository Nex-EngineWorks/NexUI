using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Core
{
    /// <summary>Registry of known screen definitions keyed by screen id.</summary>
    public sealed class UIScreenRegistry
    {
        private readonly Dictionary<string, UIScreenDefinition> _definitions =
            new Dictionary<string, UIScreenDefinition>();

        public IReadOnlyDictionary<string, UIScreenDefinition> Definitions => _definitions;

        public void Register(UIScreenDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[NexUI] Tried to register a null UIScreenDefinition.");
                return;
            }

            var id = definition.ScreenId;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError($"[NexUI] UIScreenDefinition '{definition.name}' has an empty screenId.");
                return;
            }

            if (_definitions.ContainsKey(id))
                Debug.LogWarning($"[NexUI] Duplicate screenId '{id}' registered; overwriting previous definition.");

            _definitions[id] = definition;
        }

        public bool TryGet(string screenId, out UIScreenDefinition definition)
            => _definitions.TryGetValue(screenId, out definition);

        public UIScreenDefinition Get(string screenId)
        {
            if (_definitions.TryGetValue(screenId, out var def))
                return def;
            Debug.LogError($"[NexUI] No screen registered with id '{screenId}'.");
            return null;
        }

        public bool Contains(string screenId) => _definitions.ContainsKey(screenId);

        public void Clear() => _definitions.Clear();
    }
}
