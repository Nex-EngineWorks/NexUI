using System;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Global motion event bus. Raised by motion players (built-in or DOTween) when a
    /// timeline starts / completes. Lives in Abstractions so any layer ??including
    /// integrations that must not depend on Motion ??can subscribe.
    /// </summary>
    public static class UIMotionEvents
    {
        /// <summary>(elementId, motionId) when a motion starts.</summary>
        public static event Action<string, string> Started;

        /// <summary>(elementId, motionId) when a motion completes (not on cancel).</summary>
        public static event Action<string, string> Completed;

        public static void RaiseStarted(string elementId, string motionId) => Started?.Invoke(elementId, motionId);
        public static void RaiseCompleted(string elementId, string motionId) => Completed?.Invoke(elementId, motionId);
    }
}
