using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Localization
{
    /// <summary>
    /// A table of game-facing UI strings keyed by a stable localization key, with a
    /// column per supported language. Labels bind to entries via a "loc:key" text
    /// binding. This is the *game* text table, distinct from the Designer's own UI
    /// localization.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Localization/Text Table", fileName = "NewLocalizationTable")]
    public sealed class UIGameLocalizationTable : ScriptableObject
    {
        public List<UIGameLocalizationEntry> entries = new();

        /// <summary>Returns the localized text for <paramref name="key"/> in the
        /// requested language, falling back to Korean then the key itself.</summary>
        public string Resolve(string key, string language)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.key != key)
                    continue;

                string value = language == "en-US" ? e.enUS : e.koKR;
                if (string.IsNullOrEmpty(value))
                    value = e.koKR;
                return string.IsNullOrEmpty(value) ? key : value;
            }

            return key;
        }
    }

    /// <summary>A single localization row.</summary>
    [Serializable]
    public sealed class UIGameLocalizationEntry
    {
        public string key;
        public string koKR;
        public string enUS;
    }
}
