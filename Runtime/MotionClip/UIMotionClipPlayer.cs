using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// Default <see cref="IUIMotionClipPlayer"/>. Drives an unscaled-time UniTask loop (same
    /// idiom as <c>emiteat.NexUI.Motion.BuiltInMotionPlayer</c>), evaluating every track each
    /// frame and applying the result to the resolved target's capabilities.
    /// </summary>
    public sealed class UIMotionClipPlayer : IUIMotionClipPlayer
    {
        private CancellationTokenSource _cts;

        public async UniTask PlayAsync(IUISurface surface, UIMotionClip clip, CancellationToken cancellationToken = default)
        {
            if (surface == null || clip == null || clip.tracks == null || clip.tracks.Length == 0)
                return;

            Stop();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts = cts;

            do
            {
                float elapsed = 0f;
                Evaluate(surface, clip, 0f);

                while (elapsed < clip.duration)
                {
                    bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, cts.Token).SuppressCancellationThrow();
                    if (canceled) break;
                    elapsed += Time.unscaledDeltaTime;
                    Evaluate(surface, clip, elapsed);
                }

                if (!cts.IsCancellationRequested)
                    Evaluate(surface, clip, clip.duration);
            }
            while (clip.loop && !cts.IsCancellationRequested);

            if (_cts == cts)
                _cts = null;
            cts.Dispose();
        }

        public void Stop()
        {
            if (_cts == null) return;
            var cts = _cts;
            _cts = null;
            if (!cts.IsCancellationRequested) cts.Cancel();
            cts.Dispose();
        }

        public void Evaluate(IUISurface surface, UIMotionClip clip, float time)
        {
            if (surface == null || clip?.tracks == null) return;

            foreach (var track in clip.tracks)
            {
                if (track?.propertyTracks == null) continue;
                var target = UIMotionClipTargetResolver.Resolve(surface, track.targetElementId);
                if (target == null) continue;

                foreach (var propertyTrack in track.propertyTracks)
                {
                    if (propertyTrack == null) continue;
                    var value = UIMotionClipEvaluator.Evaluate(propertyTrack, time);
                    if (value.HasValue)
                        Apply(target, propertyTrack.propertyType, value.Value);
                }
            }
        }

        private static void Apply(IUIElementHandle target, UIMotionClipPropertyType property, UIMotionClipValue value)
        {
            switch (property)
            {
                case UIMotionClipPropertyType.AnchoredPosition:
                case UIMotionClipPropertyType.LocalPosition:
                {
                    var cap = target.As<IUITransformCapability>();
                    if (cap != null) cap.Position = value.vector2Value;
                    break;
                }
                case UIMotionClipPropertyType.LocalRotationZ:
                {
                    var cap = target.As<IUITransformCapability>();
                    if (cap != null) cap.Rotation = value.floatValue;
                    break;
                }
                case UIMotionClipPropertyType.LocalScale:
                {
                    var cap = target.As<IUITransformCapability>();
                    if (cap != null) cap.Scale = value.vector3Value;
                    break;
                }
                case UIMotionClipPropertyType.SizeDelta:
                {
                    var cap = target.As<IUISizeCapability>();
                    if (cap != null) cap.SizeDelta = value.vector2Value;
                    break;
                }
                case UIMotionClipPropertyType.CanvasGroupAlpha:
                {
                    var cap = target.As<IUITransformCapability>();
                    if (cap != null) cap.Opacity = value.floatValue;
                    break;
                }
            }
        }
    }
}
