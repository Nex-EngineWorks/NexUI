using System.Threading;
using System.Threading.Tasks;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Handle to one in-flight motion playback. Lets callers await completion or stop it,
    /// and carries identity for conflict resolution and events.
    /// </summary>
    public sealed class MotionPlaybackHandle
    {
        private readonly CancellationTokenSource _cts;

        public string ElementId { get; }
        public string MotionId { get; }
        public Task Completion { get; }
        public bool IsPlaying => Completion.Status == TaskStatus.Running;

        public MotionPlaybackHandle(string elementId, string motionId, Task completion, CancellationTokenSource cts)
        {
            ElementId = elementId;
            MotionId = motionId;
            Completion = completion;
            _cts = cts;
        }

        /// <summary>Request cancellation of this playback.</summary>
        public void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                _cts.Cancel();
        }
    }
}
