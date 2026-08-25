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
                AppendTrack(seq, cap, track);
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

            // Dispose the registration before returning: UIManager disposes the linked CTS
            // right after the transition ends, and undisposed registrations would keep firing
            // against it (and accumulate on long-lived tokens).
            CancellationTokenRegistration registration = default;
            try
            {
                if (ct.CanBeCanceled)
                    registration = ct.Register(() => Stop(target));
                await tcs.Task;
            }
            finally
            {
                registration.Dispose();
            }
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

        /// <summary>
        /// Animates every keyframe of the track, not just the first and last. Each segment is
        /// a normalized 0→1 tween whose setter lerps between the surrounding keyframe values,
        /// so segment chaining stays exact regardless of when DOTween samples getters.
        /// </summary>
        private static void AppendTrack(Sequence seq, IUITransformCapability cap, UIMotionTrack track)
        {
            var keyframes = track.Keyframes;
            var duration = track.Duration <= 0f ? 0.0001f : track.Duration;

            // Snap to the authored start pose immediately (also covers delay gaps).
            ApplyValue(cap, track.Property, keyframes[0].Value);

            if (keyframes.Length == 1)
            {
                // Hold the pose for the track duration so completion still fires on time.
                seq.Insert(track.Delay, DG.Tweening.DOTween.To(() => 0f, v => { }, 0f, duration));
                return;
            }

            for (int i = 1; i < keyframes.Length; i++)
            {
                var from = keyframes[i - 1];
                var to = keyframes[i];
                float segStart = Mathf.Clamp01(from.Time) * duration + track.Delay;
                float segEnd = Mathf.Clamp01(to.Time) * duration + track.Delay;
                float segDuration = Mathf.Max(0.0001f, segEnd - segStart);

                var tween = DG.Tweening.DOTween.To(
                        () => 0f,
                        v => ApplyValue(cap, track.Property, Mathf.LerpUnclamped(from.Value, to.Value, v)),
                        1f,
                        segDuration)
                    .SetEase(track.Easing.ToDOTweenEase());
                seq.Insert(segStart, tween);
            }
        }

        private static void ApplyValue(IUITransformCapability cap, UIMotionProperty property, float value)
        {
            switch (property)
            {
                case UIMotionProperty.Opacity:
                    cap.Opacity = value;
                    break;
                case UIMotionProperty.PositionX:
                    SetPosX(cap, value);
                    break;
                case UIMotionProperty.PositionY:
                    SetPosY(cap, value);
                    break;
                case UIMotionProperty.ScaleX:
                    SetScaleX(cap, value);
                    break;
                case UIMotionProperty.ScaleY:
                    SetScaleY(cap, value);
                    break;
                case UIMotionProperty.Rotation:
                default:
                    cap.Rotation = value;
                    break;
            }
        }

        private static void SetPosX(IUITransformCapability c, float v) { var p = c.Position; p.x = v; c.Position = p; }
        private static void SetPosY(IUITransformCapability c, float v) { var p = c.Position; p.y = v; c.Position = p; }
        private static void SetScaleX(IUITransformCapability c, float v) { var s = c.Scale; s.x = v; c.Scale = s; }
        private static void SetScaleY(IUITransformCapability c, float v) { var s = c.Scale; s.y = v; c.Scale = s; }
    }
}
#endif
