#if DOTWEEN
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Motion;
using UnityEngine;

namespace emiteat.NexUI.Integrations.DOTween
{
    /// <summary>
    /// DOTween-backed <see cref="IUIMotionPlayer"/>. Drives motion purely through
    /// <see cref="IUITransformCapability"/> (no UI Toolkit / uGUI knowledge). Builds a
    /// DOTween Sequence from the compiled timeline, honours delays / parallel tracks, and
    /// kills tweens on cancellation.
    /// </summary>
    public sealed class DOTweenMotionPlayer : IUIMotionPlayer
    {
        private readonly Dictionary<IUIElementHandle, Sequence> _active =
            new Dictionary<IUIElementHandle, Sequence>();

        public async Task PlayAsync(IUIElementHandle target, UIMotionTimeline timeline, CancellationToken ct)
        {
            if (target == null || timeline?.Tracks == null || timeline.Tracks.Length == 0)
                return;

            var cap = target.As<IUITransformCapability>();
            if (cap == null)
            {
                Debug.LogWarning($"[NexUI] DOTweenMotionPlayer: '{target.Id}' has no IUITransformCapability.");
                return;
            }

            Stop(target);

            var tcs = new TaskCompletionSource<bool>();
            var seq = DG.Tweening.DOTween.Sequence();
            seq.SetUpdate(isIndependentUpdate: true); // run while paused

            foreach (var track in timeline.Tracks)
            {
                if (track == null || track.Keyframes == null || track.Keyframes.Length == 0) continue;
                float from = track.Keyframes[0].Value;
                float to = track.Keyframes[track.Keyframes.Length - 1].Value;
                var tween = MakeTween(cap, track.Property, from, to, track.Duration)
                    .SetEase(track.Easing.ToDOTweenEase());
                seq.Insert(track.Delay, tween);
            }

            MotionEvents.RaiseStarted(target.Id, timeline.MotionId);

            seq.OnComplete(() =>
            {
                _active.Remove(target);
                MotionEvents.RaiseCompleted(target.Id, timeline.MotionId);
                tcs.TrySetResult(true);
            });
            seq.OnKill(() => tcs.TrySetResult(false));

            _active[target] = seq;

            if (ct.CanBeCanceled)
                ct.Register(() => Stop(target));

            await tcs.Task;
        }

        public void Stop(IUIElementHandle target)
        {
            if (target == null) return;
            if (_active.TryGetValue(target, out var seq))
            {
                _active.Remove(target);
                if (seq != null && seq.IsActive()) seq.Kill();
            }
        }

        private static Tweener MakeTween(IUITransformCapability cap, UIMotionProperty property, float from, float to, float duration)
        {
            duration = duration <= 0f ? 0.0001f : duration;
            switch (property)
            {
                case UIMotionProperty.Opacity:
                    cap.Opacity = from;
                    return DG.Tweening.DOTween.To(() => cap.Opacity, v => cap.Opacity = v, to, duration);
                case UIMotionProperty.PositionX:
                    SetPosX(cap, from);
                    return DG.Tweening.DOTween.To(() => cap.Position.x, v => SetPosX(cap, v), to, duration);
                case UIMotionProperty.PositionY:
                    SetPosY(cap, from);
                    return DG.Tweening.DOTween.To(() => cap.Position.y, v => SetPosY(cap, v), to, duration);
                case UIMotionProperty.ScaleX:
                    SetScaleX(cap, from);
                    return DG.Tweening.DOTween.To(() => cap.Scale.x, v => SetScaleX(cap, v), to, duration);
                case UIMotionProperty.ScaleY:
                    SetScaleY(cap, from);
                    return DG.Tweening.DOTween.To(() => cap.Scale.y, v => SetScaleY(cap, v), to, duration);
                case UIMotionProperty.Rotation:
                default:
                    cap.Rotation = from;
                    return DG.Tweening.DOTween.To(() => cap.Rotation, v => cap.Rotation = v, to, duration);
            }
        }

        private static void SetPosX(IUITransformCapability c, float v) { var p = c.Position; p.x = v; c.Position = p; }
        private static void SetPosY(IUITransformCapability c, float v) { var p = c.Position; p.y = v; c.Position = p; }
        private static void SetScaleX(IUITransformCapability c, float v) { var s = c.Scale; s.x = v; c.Scale = s; }
        private static void SetScaleY(IUITransformCapability c, float v) { var s = c.Scale; s.y = v; c.Scale = s; }
    }
}
#endif
