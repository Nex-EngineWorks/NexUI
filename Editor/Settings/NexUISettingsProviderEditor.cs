using System.IO;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Settings;

namespace emiteat.NexUI.Editor.Settings
{
    /// <summary>
    /// Registers a Project Settings page for NexUI that locates (or creates) the active
    /// <see cref="NexUISettings"/> and exposes it for editing.
    /// </summary>
    public static class NexUISettingsProviderEditor
    {
        private const string ResourcesDir = "Assets/Resources";
        private const string AssetPath = "Assets/Resources/NexUISettings.asset";

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/NexUI", SettingsScope.Project)
            {
                label = "NexUI",
                guiHandler = _ =>
                {
                    var settings = FindOrNull();
                    if (settings == null)
                    {
                        EditorGUILayout.HelpBox(
                            "No NexUISettings found in a Resources folder. Create one so NexUI can " +
                            "auto-bootstrap and tools can read your configuration.", MessageType.Info);
                        if (GUILayout.Button("Create NexUISettings"))
                            CreateSettingsAsset();
                        return;
                    }

                    var editor = UnityEditor.Editor.CreateEditor(settings);
                    editor.OnInspectorGUI();
                },
                keywords = new[] { "NexUI", "UI", "Bootstrap", "Theme", "Motion" }
            };
        }

        public static void OpenSettings() => SettingsService.OpenProjectSettings("Project/NexUI");

        private static NexUISettings FindOrNull()
        {
            var guids = AssetDatabase.FindAssets("t:NexUISettings");
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<NexUISettings>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        private static void CreateSettingsAsset()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesDir))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var settings = ScriptableObject.CreateInstance<NexUISettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
        }
    }
}
