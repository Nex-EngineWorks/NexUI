using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Prompt
{
    /// <summary>The physical input device a button prompt glyph represents.</summary>
    public enum UIPromptDevice
    {
        KeyboardMouse = 0,
        Xbox = 1,
        PlayStation = 2,
        Switch = 3,
        SteamDeck = 4
    }

    /// <summary>
    /// Maps abstract action ids (e.g. "Submit", "Cancel") to a per-device glyph icon
    /// with a text fallback, so on-screen prompts follow the active input device.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Prompt/Glyph Table", fileName = "NewPromptGlyphTable")]
    public sealed class UIPromptGlyphTable : ScriptableObject
    {
        public List<UIPromptGlyphEntry> entries = new();

        /// <summary>Finds the glyph entry for an action on a device, or null.</summary>
        public UIPromptGlyphEntry Find(string actionId, UIPromptDevice device)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.actionId == actionId && e.device == device)
                    return e;
            }
            return null;
        }
    }

    /// <summary>A single action/device glyph mapping.</summary>
    [Serializable]
    public sealed class UIPromptGlyphEntry
    {
        public string actionId;
        public UIPromptDevice device;
        public Sprite icon;
        public string textFallback;
    }
}
