using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Editor.IDGenerator
{
    /// <summary>Window to pick an ID generator settings asset and emit constant classes.</summary>
    public sealed class NexUIIDGeneratorWindow : EditorWindow
    {
        private NexUIIDGeneratorSettings _settings;

        public static void Open() => GetWindow<NexUIIDGeneratorWindow>("NexUI ID Generator");

        private void OnEnable()
        {
            if (_settings == null)
            {
                var guids = AssetDatabase.FindAssets("t:NexUIIDGeneratorSettings");
                if (guids.Length > 0)
                    _settings = AssetDatabase.LoadAssetAtPath<NexUIIDGeneratorSettings>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            _settings = (NexUIIDGeneratorSettings)EditorGUILayout.ObjectField(
                "Settings", _settings, typeof(NexUIIDGeneratorSettings), false);

            using (new EditorGUI.DisabledScope(_settings == null))
            {
                if (GUILayout.Button("Generate IDs", GUILayout.Height(30)))
                    NexUIIDGenerator.Generate(_settings);
            }

            if (_settings == null)
                EditorGUILayout.HelpBox(
                    "Assign or create a NexUIIDGeneratorSettings asset (Create ??NexUI ??ID Generator Settings).",
                    MessageType.Info);
            else
                EditorGUILayout.HelpBox($"Output: {_settings.outputFolder}\nNamespace: {_settings.codeNamespace}", MessageType.None);
        }
    }
}
