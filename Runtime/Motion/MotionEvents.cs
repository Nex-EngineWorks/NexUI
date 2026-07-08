using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Motion-side convenience over the Abstractions <see cref="UIMotionEvents"/> bus.
    /// Motion players raise through here; consumers may subscribe here or on the bus.
    /// </summary>
    public static class MotionEvents
    {
        public static event Action<string, string> Started
        {
            add => UIMotionEvents.Started += value;
            remove => UIMotionEvents.Started -= value;
        }

        public static event Action<string, string> Completed
        {
            add => UIMotionEvents.Completed += value;
            remove => UIMotionEvents.Completed -= value;
        }

        public static void RaiseStarted(string elementId, string motionId)
            => UIMotionEvents.RaiseStarted(elementId, motionId);

        public static void RaiseCompleted(string elementId, string motionId)
            => UIMotionEvents.RaiseCompleted(elementId, motionId);
    }
}
