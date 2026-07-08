using UnityEngine;
using emiteat.NexUI.Core.Registry;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Editor.IDGenerator
{
    /// <summary>
    /// Inputs for the ID generator. Point it at your registries plus any manual state /
    /// action / element key lists, and it emits strongly-typed constant classes.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/ID Generator Settings", fileName = "NexUIIDGeneratorSettings")]
    public sealed class NexUIIDGeneratorSettings : ScriptableObject
    {
        [Header("Output")]
        public string outputFolder = "Assets/NexUI/Generated";
        public string codeNamespace = "emiteat.NexUI.Generated";

        [Header("Sources")]
        public UIScreenRegistryAsset screenRegistry;
        public UIMotionRegistryAsset motionRegistry;
        public UIThemeRegistryAsset themeRegistry;

        [Header("Manual keys")]
        public string[] stateKeys = System.Array.Empty<string>();
        public string[] actionKeys = System.Array.Empty<string>();
        public string[] elementIds = System.Array.Empty<string>();
    }
}
