using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Debugging
{
    /// <summary>Immutable-ish capture of NexUI runtime state for inspection / overlay.</summary>
    public sealed class NexUIDebugSnapshot
    {
        public DateTime CapturedAtUtc = DateTime.UtcNow;

        public List<ScreenInfo> OpenScreens = new List<ScreenInfo>();
        public List<string> BackStack = new List<string>();
        public List<string> ModalStack = new List<string>();
        public int ToastQueueCount;
        public string FocusedElementId;

        public List<string> RegisteredBackends = new List<string>();
        public List<string> StateKeys = new List<string>();
        public List<string> ActionKeys = new List<string>();
        public List<string> RecentCommands = new List<string>();
        public int QueryCacheCount;
        public string ActiveThemeId;

        public struct ScreenInfo
        {
            public string screenId;
            public string layer;
            public string state;
            public string backend;
        }
    }
}
