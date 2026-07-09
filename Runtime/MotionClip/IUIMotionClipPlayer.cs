using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// Runtime playback contract for <see cref="UIMotionClip"/>. Has no editor dependency —
    /// implementations only touch <see cref="IUISurface"/>/<see cref="IUIElementHandle"/>
    /// capabilities, so the same implementation drives both runtime playback and the Motion
    /// Clip Editor's in-Designer preview.
    /// </summary>
    public interface IUIMotionClipPlayer
    {
        UniTask PlayAsync(IUISurface surface, UIMotionClip clip, CancellationToken cancellationToken = default);
        void Stop();
        void Evaluate(IUISurface surface, UIMotionClip clip, float time);
    }
}
