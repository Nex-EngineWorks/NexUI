using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Settings
{
    /// <summary>
    /// Project-wide NexUI configuration asset. Lets a project initialize screens, themes,
    /// motions and runtime features from data instead of code. Lives in the Settings
    /// assembly, which may reference Core/Motion/Theme (Core itself may not).
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Settings", fileName = "NexUISettings")]
    public sealed class NexUISettings : ScriptableObject
    {
        [Header("Bootstrap")]
        public NexUIBootstrapMode bootstrapMode = NexUIBootstrapMode.Manual;
        public UIRenderBackend defaultBackend = UIRenderBackend.UGUI;

        [Header("Features")]
        public bool useBuiltInMotionPlayer = true;
        public bool enableDebugOverlay;
        public bool enableQuery;
        public bool enableCommandLog;
        public bool enableValidationOnStart;

        [Header("Content")]
        public UIScreenDefinition[] screens = System.Array.Empty<UIScreenDefinition>();
        public UITheme[] themes = System.Array.Empty<UITheme>();
        public UIMotionPreset[] motions = System.Array.Empty<UIMotionPreset>();

        [Header("Layers")]
        public UILayerRootConfig[] layerRoots = System.Array.Empty<UILayerRootConfig>();
    }
}
