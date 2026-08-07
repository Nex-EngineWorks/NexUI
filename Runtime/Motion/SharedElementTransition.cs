using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Animates a "shared element" from a source element's transform to a destination's,
    /// approximating a hero transition. Backend-independent: reads both endpoints through
    /// <see cref="IUITransformCapability"/> and animates the destination.
    /// </summary>
    public static class SharedElementTransition
    {
        public static Task PlayAsync(
            IUIMotionPlayer player,
            IUIElementHandle source,
            IUIElementHandle destination,
            float duration,
            CancellationToken ct = default)
        {
            var src = source?.As<IUITransformCapability>();
            var dst = destination?.As<IUITransformCapability>();
            if (player == null || src == null || dst == null)
                return Task.CompletedTask;

            Vector2 from = src.Position;
            Vector2 to = dst.Position;
            var timeline = LayoutMotion.Move(from, to, duration);
            timeline.MotionId = "shared-element";
            return player.PlayAsync(destination, timeline, ct);
        }
    }
}
