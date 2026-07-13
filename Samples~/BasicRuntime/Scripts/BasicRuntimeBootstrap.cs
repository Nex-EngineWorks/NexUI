using System;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Motion;

namespace emiteat.NexUI.Samples.BasicRuntime
{
    /// <summary>
    /// Builds a few screen definitions in code (so the sample needs no pre-authored
    /// .asset files) and opens the HUD. Assign either UI Toolkit VisualTreeAssets or uGUI
    /// prefabs in the inspector; the backend is chosen to match what you assign.
    ///
    /// Put this on a GameObject that also has the matching backend bootstrap:
    ///  - UI Toolkit: a UIDocument + UIToolkitIntegrationBootstrap
    ///  - uGUI:       a Canvas + UGUIIntegrationBootstrap
    /// </summary>
    public sealed class BasicRuntimeBootstrap : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private UIRenderBackend _backend = UIRenderBackend.UGUI;

        [Header("Screen assets (VisualTreeAsset for UI Toolkit, prefab for uGUI)")]
        [SerializeField] private UnityEngine.Object _hudAsset;
        [SerializeField] private UnityEngine.Object _inventoryAsset;
        [SerializeField] private UnityEngine.Object _pauseMenuAsset;

        [Header("Motion")]
        [SerializeField] private UIMotionPreset _popupMotion;
        [SerializeField] private UIMotionPreset _fadeMotion;

        public UIScreenDefinition Hud { get; private set; }
        public UIScreenDefinition Inventory { get; private set; }
        public UIScreenDefinition PauseMenu { get; private set; }

        private async void Start()
        {
            // A code-built popup motion so the sample animates even without an asset.
            var popup = _popupMotion != null ? _popupMotion : BuildDefaultPopupMotion();
            var fade = _fadeMotion != null ? _fadeMotion : BuildDefaultFadeMotion();

            Hud = BuildDefinition("HUD", _hudAsset, UILayerType.HUD, UIOpenPolicy.Single, null, null);
            Inventory = BuildDefinition("Inventory", _inventoryAsset, UILayerType.Window, UIOpenPolicy.Single, popup, fade);
            PauseMenu = BuildDefinition("PauseMenu", _pauseMenuAsset, UILayerType.Modal, UIOpenPolicy.StackPush, popup, fade);

            Core.NexUIApp.RegisterScreen(Hud);
            Core.NexUIApp.RegisterScreen(Inventory);
            Core.NexUIApp.RegisterScreen(PauseMenu);

            if (_hudAsset != null)
                await Core.NexUIApp.OpenAsync("HUD");
            else
                Debug.LogWarning("[NexUI Sample] Assign a HUD asset to see it open.");
        }

        private UIScreenDefinition BuildDefinition(
            string id, UnityEngine.Object asset, UILayerType layer, UIOpenPolicy openPolicy,
            UIMotionPreset open, UIMotionPreset close)
        {
            var def = ScriptableObject.CreateInstance<UIScreenDefinition>();
            def.name = id;
            def.identity = new UIScreenIdentity { screenId = id, priority = 0 };
            def.backendAsset = new UIScreenBackendAsset { backend = _backend, asset = asset };
            def.layer = new UIScreenLayerConfig { layerType = layer, openPolicy = openPolicy };
            def.motion = new UIScreenMotionConfig { openMotion = open, closeMotion = close };
            def.focus = new UIScreenFocusConfig
            {
                trapFocus = layer == UILayerType.Modal,
                restoreFocusOnClose = true
            };
            def.policy = new UIScreenPolicyConfig
            {
                blockInputBehind = layer == UILayerType.Modal,
                closeOnBack = true,
                cursorPolicy = CursorPolicy.Unchanged
            };
            return def;
        }

        private static UIMotionPreset BuildDefaultPopupMotion()
        {
            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            preset.motionId = "sample.popup";
            preset.defaultVariant = "default";
            preset.variants = new[]
            {
                new UIMotionVariant
                {
                    name = "default",
                    steps = new[]
                    {
                        UIMotionStep.Fade(0f, 1f, 0.2f),
                        new UIMotionStep
                        {
                            property = UIMotionProperty.ScaleX, from = 0.9f, to = 1f,
                            duration = 0.2f, easing = UIMotionEasing.EaseInOut
                        },
                        new UIMotionStep
                        {
                            property = UIMotionProperty.ScaleY, from = 0.9f, to = 1f,
                            duration = 0.2f, easing = UIMotionEasing.EaseInOut
                        },
                    }
                }
            };
            return preset;
        }

        private static UIMotionPreset BuildDefaultFadeMotion()
        {
            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            preset.motionId = "sample.fade";
            preset.variants = new[]
            {
                new UIMotionVariant { name = "default", steps = new[] { UIMotionStep.Fade(1f, 0f, 0.15f) } }
            };
            return preset;
        }
    }
}
