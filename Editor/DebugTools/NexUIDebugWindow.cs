using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Debugging;

namespace emiteat.NexUI.Editor.DebugTools
{
    /// <summary>
    /// Editor window that captures and displays a <see cref="NexUIDebugSnapshot"/> during
    /// play mode, and toggles the runtime overlay.
    /// </summary>
    public sealed class NexUIDebugWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/NexUI/Debug Snapshot")]
        public static void Open() => GetWindow<NexUIDebugWindow>("NexUI Debug");

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Toggle Runtime Overlay")) NexUIDebug.ToggleOverlay();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter play mode to capture a runtime snapshot.", MessageType.Info);
                return;
            }

            var snap = NexUIDebug.Capture();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Open Screens", EditorStyles.boldLabel);
            foreach (var s in snap.OpenScreens)
                EditorGUILayout.LabelField($"  {s.screenId} [{s.layer}/{s.backend}] {s.state}");

            EditorGUILayout.LabelField("Back Stack", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  " + string.Join(", ", snap.BackStack));
            EditorGUILayout.LabelField("Modal Stack", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  " + string.Join(", ", snap.ModalStack));
            EditorGUILayout.LabelField($"Toast queue: {snap.ToastQueueCount}   Focus: {snap.FocusedElementId}");

            EditorGUILayout.LabelField("State Keys", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  " + string.Join(", ", snap.StateKeys));
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  " + string.Join(", ", snap.ActionKeys));

            EditorGUILayout.LabelField("Recent Commands", EditorStyles.boldLabel);
            foreach (var c in snap.RecentCommands) EditorGUILayout.LabelField("  " + c);

            EditorGUILayout.LabelField($"Query cache: {snap.QueryCacheCount}   Theme: {snap.ActiveThemeId}");

            EditorGUILayout.EndScrollView();
        }
    }
}
