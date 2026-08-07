using System.Threading;
using System.Threading.Tasks;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Plays a compiled <see cref="UIMotionTimeline"/> against an element handle.
    /// The player drives motion exclusively through <see cref="IUITransformCapability"/>,
    /// so it never needs to know about UI Toolkit or uGUI.
    /// </summary>
    public interface IUIMotionPlayer
    {
        Task PlayAsync(
            IUIElementHandle target,
            UIMotionTimeline timeline,
            CancellationToken ct
        );

        void Stop(IUIElementHandle target);
    }
}
