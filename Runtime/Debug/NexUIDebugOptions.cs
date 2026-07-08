using UnityEngine;

namespace emiteat.NexUI.Debugging
{
    /// <summary>Controls what the debug overlay/snapshot captures and how it renders.</summary>
    public sealed class NexUIDebugOptions
    {
        public bool captureScreens = true;
        public bool captureStacks = true;
        public bool captureStateKeys = true;
        public bool captureActions = true;
        public bool captureCommandLog = true;
        public bool captureMotions = true;
        public bool captureQuery = true;
        public bool captureTheme = true;

        public KeyCode toggleKey = KeyCode.F9;
        public int maxCommandLogLines = 20;

        public static NexUIDebugOptions Default => new NexUIDebugOptions();
    }
}
