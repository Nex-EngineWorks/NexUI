using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Command;
using emiteat.NexUI.Query;
using emiteat.NexUI.State;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Debugging
{
    /// <summary>
    /// Collects a <see cref="NexUIDebugSnapshot"/> from the live NexUI runtime. Optional
    /// sources (state store, actions, command log, query cache) are supplied by the host so
    /// the Debug module stays decoupled from how the game wires them.
    /// </summary>
    public sealed class NexUIDebugService
    {
        public UIManager Manager;
        public UIStateStore StateStore;
        public UIActionResolver Actions;
        public CommandLog CommandLog;
        public QueryCache QueryCache;
        public NexUIDebugOptions Options = NexUIDebugOptions.Default;

        public NexUIDebugSnapshot Capture()
        {
            var snap = new NexUIDebugSnapshot();
            var m = Manager;
            if (m == null) return snap;

            if (Options.captureScreens)
            {
                foreach (var inst in m.OpenScreens)
                {
                    snap.OpenScreens.Add(new NexUIDebugSnapshot.ScreenInfo
                    {
                        screenId = inst.ScreenId,
                        layer = inst.Layer.ToString(),
                        state = inst.State.ToString(),
                        backend = inst.Surface != null ? inst.Surface.Backend.ToString() : "?"
                    });
                }
            }

            if (Options.captureStacks)
            {
                snap.BackStack.AddRange(m.BackStackSnapshot());
                snap.ModalStack.AddRange(m.ModalStackSnapshot());
                snap.ToastQueueCount = m.ToastQueueCount;
                snap.FocusedElementId = m.LastFocusedElementId;
                foreach (var b in m.RegisteredBackends) snap.RegisteredBackends.Add(b.ToString());
            }

            if (Options.captureStateKeys && StateStore != null)
                foreach (var k in StateStore.Keys) snap.StateKeys.Add(k);

            if (Options.captureActions && Actions != null)
                foreach (var k in Actions.Keys) snap.ActionKeys.Add(k);

            if (Options.captureCommandLog && CommandLog != null)
            {
                int start = System.Math.Max(0, CommandLog.Count - Options.maxCommandLogLines);
                for (int i = start; i < CommandLog.Count; i++)
                    snap.RecentCommands.Add(CommandLog.Entries[i].CommandId);
            }

            if (Options.captureQuery && QueryCache != null)
                snap.QueryCacheCount = QueryCache.Count;

            if (Options.captureTheme)
                snap.ActiveThemeId = NexUITheme.Active != null ? NexUITheme.Active.themeId : "(none)";

            return snap;
        }
    }
}
