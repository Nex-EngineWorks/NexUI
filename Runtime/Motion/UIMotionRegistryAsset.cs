using System;
using UnityEngine;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Central authoring list of motion presets. Placed in the Motion assembly (not Core)
    /// so it can hold strongly-typed <see cref="UIMotionPreset"/> references without Core
    /// depending on Motion. Read by validators and the ID generator.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Registry/Motion Registry", fileName = "MotionRegistry")]
    public sealed class UIMotionRegistryAsset : ScriptableObject
    {
        public UIMotionPreset[] motions = Array.Empty<UIMotionPreset>();

        public bool TryGet(string motionId, out UIMotionPreset preset)
        {
            if (motions != null)
            {
                foreach (var m in motions)
                    if (m != null && m.motionId == motionId) { preset = m; return true; }
            }
            preset = null;
            return false;
        }
    }
}
