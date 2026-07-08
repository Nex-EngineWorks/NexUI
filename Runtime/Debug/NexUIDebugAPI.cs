using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Command;
using emiteat.NexUI.Query;
using emiteat.NexUI.State;
using UnityEngine;

namespace emiteat.NexUI.Debugging
{
    /// <summary>
    /// Static entry point for the runtime debug overlay / snapshot. Wire optional sources
    /// once via <see cref="Configure"/>, then Show/Hide/Toggle or Capture at will.
    /// </summary>
    public static class NexUIDebug
    {
        private static readonly NexUIDebugService _service = new NexUIDebugService();
        private static NexUIDebugOverlay _overlay;

        public static NexUIDebugService Service => _service;

        public static void Configure(
            UIManager manager = null,
            UIStateStore stateStore = null,
            UIActionResolver actions = null,
            CommandLog commandLog = null,
            QueryCache queryCache = null,
            NexUIDebugOptions options = null)
        {
            if (manager != null) _service.Manager = manager;
            if (stateStore != null) _service.StateStore = stateStore;
            if (actions != null) _service.Actions = actions;
            if (commandLog != null) _service.CommandLog = commandLog;
            if (queryCache != null) _service.QueryCache = queryCache;
            if (options != null) _service.Options = options;
        }

        public static NexUIDebugSnapshot Capture()
        {
            if (_service.Manager == null)
                _service.Manager = Core.NexUI.Manager;
            return _service.Capture();
        }

        public static void ShowOverlay() => SetOverlay(true);
        public static void HideOverlay() => SetOverlay(false);
        public static void ToggleOverlay() => SetOverlay(_overlay == null || !_overlay.Visible);

        private static void SetOverlay(bool visible)
        {
            EnsureOverlay();
            _overlay.Visible = visible;
        }

        private static void EnsureOverlay()
        {
            if (_overlay != null) return;
            if (_service.Manager == null) _service.Manager = Core.NexUI.Manager;

            var go = new GameObject("[NexUI] DebugOverlay") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _overlay = go.AddComponent<NexUIDebugOverlay>();
            _overlay.Service = _service;
        }
    }
}
