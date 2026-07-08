using System.IO;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Registry;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;
using emiteat.NexUI.Settings;

namespace emiteat.NexUI.Editor.ProjectSetup
{
    /// <summary>Creates the default NexUI asset set. Pure editor utility (not a Designer).</summary>
    public static class NexUIProjectAssetCreator
    {
        public const string RootFolder = "Assets/NexUI";

        public static void EnsureFolders()
        {
            EnsureFolder("Assets", "NexUI");
            foreach (var sub in new[] { "Settings", "Screens", "Motions", "Themes", "Registries", "Generated" })
                EnsureFolder(RootFolder, sub);
        }

        public static NexUISettings CreateSettings()
            => CreateAsset<NexUISettings>($"{RootFolder}/Settings/NexUISettings.asset");

        public static UIScreenRegistryAsset CreateScreenRegistry()
            => CreateAsset<UIScreenRegistryAsset>($"{RootFolder}/Registries/ScreenRegistry.asset");

        public static UIMotionRegistryAsset CreateMotionRegistry()
            => CreateAsset<UIMotionRegistryAsset>($"{RootFolder}/Registries/MotionRegistry.asset");

        public static UIThemeRegistryAsset CreateThemeRegistry()
            => CreateAsset<UIThemeRegistryAsset>($"{RootFolder}/Registries/ThemeRegistry.asset");

        public static UITheme CreateDefaultTheme()
        {
            var theme = CreateAsset<UITheme>($"{RootFolder}/Themes/DefaultTheme.asset");
            theme.themeId = "default";
            theme.tokens = new[]
            {
                new ThemeToken("color.bg", "#101014"),
                new ThemeToken("color.surface", "#1E1E22"),
                new ThemeToken("color.primary", "#3B82F6"),
                new ThemeToken("color.danger", "#DC2626"),
                new ThemeToken("color.text", "#F5F5F5"),
                new ThemeToken("radius.md", "8"),
            };
            EditorUtility.SetDirty(theme);
            return theme;
        }

        public static UIMotionPreset CreateDefaultMotion()
        {
            var preset = CreateAsset<UIMotionPreset>($"{RootFolder}/Motions/PopupIn.asset");
            preset.motionId = "PopupIn";
            preset.variants = new[]
            {
                new UIMotionVariant { name = "default", steps = new[] { UIMotionStep.Fade(0f, 1f, 0.2f) } }
            };
            EditorUtility.SetDirty(preset);
            return preset;
        }

        public static UIScreenDefinition CreateScreen(string screenId, UILayerType layer, UIOpenPolicy policy)
        {
            var def = CreateAsset<UIScreenDefinition>($"{RootFolder}/Screens/{screenId}.asset");
            def.identity = new UIScreenIdentity { screenId = screenId };
            def.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
            def.layer = new UIScreenLayerConfig { layerType = layer, openPolicy = policy };
            EditorUtility.SetDirty(def);
            return def;
        }

        // ---- helpers --------------------------------------------------------

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var full = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
