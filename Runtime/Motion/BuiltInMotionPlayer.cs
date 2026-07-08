using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Fallback motion player. Drives a compiled <see cref="UIMotionTimeline"/> against an
    /// element's <see cref="IUITransformCapability"/> using an unscaled-time UniTask loop, so
    /// animations still run while the game is paused. Supports Opacity / Position / Scale /
    /// Rotation with Linear and EaseInOut easing.
    ///
    /// It never touches a UI backend. Advanced easing, sequencing and blending are deferred
    /// to the DOTween integration.
    /// </summary>
    public sealed class BuiltInMotionPlayer : IUIMotionPlayer
    {
        private readonly Dictionary<IUIElementHandle, CancellationTokenSource> _active =
            new Dictionary<IUIElementHandle, CancellationTokenSource>();

        public async UniTask PlayAsync(IUIElementHandle target, UIMotionTimeline timeline, CancellationToken ct)
        {
            if (target == null || timeline?.Tracks == null || timeline.Tracks.Length == 0)
                return;

            var cap = target.As<IUITransformCapability>();
            if (cap == null)
            {
                Debug.LogWarning(
                    $"[NexUI] BuiltInMotionPlayer: element '{target.Id}' has no IUITransformCapability; motion skipped.");
                return;
            }

            Stop(target);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _active[target] = cts;

            float total = timeline.TotalDuration;
            float elapsed = 0f;

            MotionEvents.RaiseStarted(target.Id, timeline.MotionId);
            ApplyAt(cap, timeline, 0f);

            while (elapsed < total)
            {
                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, cts.Token).SuppressCancellationThrow();
                if (canceled) break;
                elapsed += Time.unscaledDeltaTime;
                ApplyAt(cap, timeline, elapsed);
            }

            bool wasCanceled = cts.IsCancellationRequested;
            if (!wasCanceled)
            {
                ApplyAt(cap, timeline, total);
                MotionEvents.RaiseCompleted(target.Id, timeline.MotionId);
            }

            if (_active.TryGetValue(target, out var current) && current == cts)
                _active.Remove(target);
            cts.Dispose();
        }

        public void Stop(IUIElementHandle target)
        {
            if (target == null) return;
            if (_active.TryGetValue(target, out var cts))
            {
                _active.Remove(target);
                if (!cts.IsCancellationRequested) cts.Cancel();
                cts.Dispose();
            }
        }

        private static void ApplyAt(IUITransformCapability cap, UIMotionTimeline timeline, float time)
        {
            foreach (var track in timeline.Tracks)
            {
                if (track == null) continue;
                float local = time - track.Delay;
                if (local < 0f) local = 0f;
                float n = track.Duration <= 0f ? 1f : Mathf.Clamp01(local / track.Duration);
                float eased = Ease(track.Easing, n);
                float value = Evaluate(track, eased);
                ApplyProperty(cap, track.Property, value);
            }
        }

        private static float Evaluate(UIMotionTrack track, float t)
        {
            var k = track.Keyframes;
            if (k == null || k.Length == 0) return 0f;
            if (k.Length == 1) return k[0].Value;

            for (int i = 0; i < k.Length - 1; i++)
            {
                if (t <= k[i + 1].Time)
                {
                    float span = k[i + 1].Time - k[i].Time;
                    float localT = span <= 0f ? 0f : (t - k[i].Time) / span;
                    return Mathf.Lerp(k[i].Value, k[i + 1].Value, localT);
                }
            }
            return k[k.Length - 1].Value;
        }

        private static float Ease(UIMotionEasing easing, float t)
        {
            switch (easing)
            {
                case UIMotionEasing.EaseInOut:
                    return t * t * (3f - 2f * t);
                case UIMotionEasing.Linear:
                default:
                    return t;
            }
        }

        private static void ApplyProperty(IUITransformCapability cap, UIMotionProperty property, float value)
        {
            switch (property)
            {
                case UIMotionProperty.Opacity:
                    cap.Opacity = value;
                    break;
                case UIMotionProperty.PositionX:
                {
                    var p = cap.Position; p.x = value; cap.Position = p;
                    break;
                }
                case UIMotionProperty.PositionY:
                {
                    var p = cap.Position; p.y = value; cap.Position = p;
                    break;
                }
                case UIMotionProperty.ScaleX:
                {
                    var s = cap.Scale; s.x = value; cap.Scale = s;
                    break;
                }
                case UIMotionProperty.ScaleY:
                {
                    var s = cap.Scale; s.y = value; cap.Scale = s;
                    break;
                }
                case UIMotionProperty.Rotation:
                    cap.Rotation = value;
                    break;
            }
        }
    }
}
