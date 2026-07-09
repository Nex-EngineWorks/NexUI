using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// Adds <see cref="UIMotionClip"/> playback to <see cref="UIManager"/> without modifying it
    /// (Core has no reference to this module, matching the existing "Core only depends on
    /// Abstractions" seam — this is a leaf extension, not a Core change).
    /// </summary>
    public static class UIManagerMotionClipExtensions
    {
        private static readonly IUIMotionClipPlayer SharedPlayer = new UIMotionClipPlayer();

        /// <summary>Plays <paramref name="clip"/> against the currently open screen's surface.</summary>
        public static UniTask PlayMotionClipAsync(this UIManager manager, string screenId, UIMotionClip clip, CancellationToken ct = default)
        {
            var surface = manager?.GetSurface(screenId);
            return surface == null ? UniTask.CompletedTask : SharedPlayer.PlayAsync(surface, clip, ct);
        }
    }
}
