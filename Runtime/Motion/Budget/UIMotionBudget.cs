using System;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Caps how many motions may play at once and how aggressively low-priority /
    /// non-essential motions are skipped. Consumed by the motion player to keep the
    /// UI within a performance / accessibility budget.
    /// </summary>
    [Serializable]
    public sealed class UIMotionBudget
    {
        public int maxConcurrentMotions = 32;
        public bool reduceMotion;
        public bool skipLowPriorityMotions;
    }

    /// <summary>
    /// Relative importance of a motion. Under budget pressure or reduce-motion,
    /// lower priorities are dropped first; <see cref="Critical"/> always plays.
    /// </summary>
    public enum UIMotionPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}
