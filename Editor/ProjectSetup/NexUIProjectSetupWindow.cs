using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Editor.ProjectSetup
{
    /// <summary>Editor window driving <see cref="NexUIProjectSetupWizard"/>.</summary>
    public sealed class NexUIProjectSetupWindow : EditorWindow
    {
        private readonly NexUIProjectSetupWizard _wizard = new NexUIProjectSetupWizard();

        public static void Open() => GetWindow<NexUIProjectSetupWindow>("NexUI Project Setup");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("NexUI Project Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates NexUI settings, registries, a default theme/motion and starter screen " +
                "definitions under Assets/NexUI. This is a project bootstrap utility, not a Designer.",
                MessageType.Info);

            _wizard.createSettings = EditorGUILayout.Toggle("Create Settings", _wizard.createSettings);
            _wizard.createRegistries = EditorGUILayout.Toggle("Create Registries", _wizard.createRegistries);
            _wizard.createDefaultTheme = EditorGUILayout.Toggle("Create Default Theme", _wizard.createDefaultTheme);
            _wizard.createDefaultMotion = EditorGUILayout.Toggle("Create Default Motion", _wizard.createDefaultMotion);
            _wizard.createDefaultScreens = EditorGUILayout.Toggle("Create Starter Screens", _wizard.createDefaultScreens);

            EditorGUILayout.Space();
            if (GUILayout.Button("Run Setup", GUILayout.Height(32)))
                _wizard.Run();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("After setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Import samples from Package Manager (NexUI ??Samples), then add a backend " +
                "bootstrap (UIToolkitIntegrationBootstrap or UGUIIntegrationBootstrap) to your scene.",
                MessageType.None);
        }
    }
}
