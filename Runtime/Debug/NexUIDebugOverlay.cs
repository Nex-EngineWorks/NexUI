using System.Text;
using UnityEngine;

namespace emiteat.NexUI.Debugging
{
    /// <summary>
    /// IMGUI fallback overlay that renders a <see cref="NexUIDebugSnapshot"/>. IMGUI is used
    /// deliberately so the Debug module needs no UI Toolkit / uGUI dependency. A richer
    /// UI Toolkit renderer could live in Integrations.UIToolkit later.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class NexUIDebugOverlay : MonoBehaviour
    {
        public NexUIDebugService Service;
        public bool Visible;

        private Vector2 _scroll;
        private GUIStyle _style;

        private void Update()
        {
            if (Service?.Options != null && Input.GetKeyDown(Service.Options.toggleKey))
                Visible = !Visible;
        }

        private void OnGUI()
        {
            if (!Visible || Service == null) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };

            var snap = Service.Capture();
            const float w = 380f;
            GUILayout.BeginArea(new Rect(8, 8, w, Screen.height - 16), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("<b>NexUI Debug Overlay</b>", _style);
            GUILayout.Label($"Captured: {snap.CapturedAtUtc:HH:mm:ss} UTC", _style);
            GUILayout.Space(4);

            Section("Open Screens", _style);
            foreach (var s in snap.OpenScreens)
                GUILayout.Label($"  {s.screenId}  [{s.layer}/{s.backend}] {s.state}", _style);

            Section("Back Stack", _style);
            GUILayout.Label("  " + Join(snap.BackStack), _style);
            Section("Modal Stack", _style);
            GUILayout.Label("  " + Join(snap.ModalStack), _style);
            GUILayout.Label($"  Toast queue: {snap.ToastQueueCount}   Focus: {snap.FocusedElementId}", _style);
            GUILayout.Label($"  Backends: {Join(snap.RegisteredBackends)}", _style);

            Section("State Keys", _style);
            GUILayout.Label("  " + Join(snap.StateKeys), _style);
            Section("Actions", _style);
            GUILayout.Label("  " + Join(snap.ActionKeys), _style);

            Section("Recent Commands", _style);
            foreach (var c in snap.RecentCommands) GUILayout.Label("  " + c, _style);

            GUILayout.Label($"Query cache: {snap.QueryCacheCount}   Theme: {snap.ActiveThemeId}", _style);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void Section(string title, GUIStyle style)
        {
            GUILayout.Space(4);
            GUILayout.Label($"<b>{title}</b>", style);
        }

        private static string Join(System.Collections.Generic.List<string> items)
            => (items == null || items.Count == 0) ? "-" : string.Join(", ", items);
    }
}
