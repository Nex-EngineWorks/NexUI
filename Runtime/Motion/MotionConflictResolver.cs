using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>How to resolve a new motion request that arrives while one is playing.</summary>
    public enum MotionConflictPolicy
    {
        /// <summary>Stop the running motion and start the new one immediately.</summary>
        Interrupt = 0,

        /// <summary>Defer the new motion until the running one finishes.</summary>
        Queue = 1,

        /// <summary>Ignore the new request while one is running.</summary>
        Ignore = 2,

        /// <summary>Blend from the current transform into the new motion (approximate).</summary>
        Blend = 3
    }

    /// <summary>
    /// Tracks which elements currently have a running motion and decides, per
    /// <see cref="MotionConflictPolicy"/>, whether a new request may proceed. Local to the
    /// Motion module (does not reference Core's transition policy enum).
    /// </summary>
    public sealed class MotionConflictResolver
    {
        private readonly IUIMotionPlayer _player;
        private readonly HashSet<IUIElementHandle> _busy = new HashSet<IUIElementHandle>();
        private readonly Dictionary<IUIElementHandle, System.Action> _queued =
            new Dictionary<IUIElementHandle, System.Action>();

        public MotionConflictResolver(IUIMotionPlayer player) => _player = player;

        public bool IsBusy(IUIElementHandle target) => target != null && _busy.Contains(target);

        public void MarkStarted(IUIElementHandle target)
        {
            if (target != null) _busy.Add(target);
        }

        public void MarkFinished(IUIElementHandle target)
        {
            if (target == null) return;
            _busy.Remove(target);
            if (_queued.TryGetValue(target, out var next))
            {
                _queued.Remove(target);
                next?.Invoke();
            }
        }

        /// <summary>
        /// Decide whether <paramref name="startNext"/> may run now. Returns true to proceed
        /// immediately (interrupting/blending as needed), false if dropped or queued.
        /// </summary>
        public bool Resolve(IUIElementHandle target, MotionConflictPolicy policy, System.Action startNext)
        {
            if (target == null) return false;
            if (!IsBusy(target)) return true;

            switch (policy)
            {
                case MotionConflictPolicy.Interrupt:
                case MotionConflictPolicy.Blend:
                    // Blend currently degrades to interrupt (the built-in player has no
                    // blend tree); the DOTween integration can refine this later.
                    _player?.Stop(target);
                    _busy.Remove(target);
                    return true;

                case MotionConflictPolicy.Queue:
                    _queued[target] = startNext;
                    return false;

                case MotionConflictPolicy.Ignore:
                default:
                    return false;
            }
        }
    }
}
