using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Fallback motion player.
    /// Drives a compiled UIMotionTimeline using unscaled Unity time.
    /// </summary>
    public sealed class BuiltInMotionPlayer : IUIMotionPlayer
    {
        private readonly Dictionary<IUIElementHandle, CancellationTokenSource> _active =
            new Dictionary<IUIElementHandle, CancellationTokenSource>();

        public async Task PlayAsync(
            IUIElementHandle target,
            UIMotionTimeline timeline,
            CancellationToken ct)
        {
            if (target == null ||
                timeline?.Tracks == null ||
                timeline.Tracks.Length == 0)
            {
                return;
            }

            var capability = target.As<IUITransformCapability>();

            if (capability == null)
            {
                Debug.LogWarning(
                    $"[NexUI] BuiltInMotionPlayer: element '{target.Id}' " +
                    "has no IUITransformCapability; motion skipped.");

                return;
            }

            Stop(target);

            var cts =
                CancellationTokenSource.CreateLinkedTokenSource(ct);

            _active[target] = cts;

            try
            {
                var total = timeline.TotalDuration;
                var elapsed = 0f;

                MotionEvents.RaiseStarted(
                    target.Id,
                    timeline.MotionId);

                ApplyAt(
                    capability,
                    timeline,
                    0f);

                while (elapsed < total)
                {
                    await Task.Yield();

                    if (cts.IsCancellationRequested)
                    {
                        break;
                    }

                    elapsed += UnityTime.unscaledDeltaTime;

                    ApplyAt(
                        capability,
                        timeline,
                        Mathf.Min(elapsed, total));
                }

                if (!cts.IsCancellationRequested)
                {
                    ApplyAt(
                        capability,
                        timeline,
                        total);

                    MotionEvents.RaiseCompleted(
                        target.Id,
                        timeline.MotionId);
                }
            }
            finally
            {
                if (_active.TryGetValue(target, out var current) &&
                    ReferenceEquals(current, cts))
                {
                    _active.Remove(target);
                }

                cts.Dispose();
            }
        }

        public void Stop(IUIElementHandle target)
        {
            if (target == null)
            {
                return;
            }

            if (!_active.TryGetValue(target, out var cts))
            {
                return;
            }

            _active.Remove(target);

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }

            // 여기서 Dispose하지 않는다.
            // 실행 중인 PlayAsync의 finally에서 Dispose한다.
        }

        private static void ApplyAt(
            IUITransformCapability capability,
            UIMotionTimeline timeline,
            float time)
        {
            foreach (var track in timeline.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                var localTime = time - track.Delay;

                if (localTime < 0f)
                {
                    localTime = 0f;
                }

                var normalizedTime =
                    track.Duration <= 0f
                        ? 1f
                        : Mathf.Clamp01(
                            localTime / track.Duration);

                var eased =
                    Ease(
                        track.Easing,
                        normalizedTime);

                var value =
                    Evaluate(
                        track,
                        eased);

                ApplyProperty(
                    capability,
                    track.Property,
                    value);
            }
        }

        private static float Evaluate(
            UIMotionTrack track,
            float time)
        {
            var keyframes = track.Keyframes;

            if (keyframes == null ||
                keyframes.Length == 0)
            {
                return 0f;
            }

            if (keyframes.Length == 1)
            {
                return keyframes[0].Value;
            }

            for (var i = 0; i < keyframes.Length - 1; i++)
            {
                if (time > keyframes[i + 1].Time)
                {
                    continue;
                }

                var span =
                    keyframes[i + 1].Time -
                    keyframes[i].Time;

                var localTime =
                    span <= 0f
                        ? 0f
                        : (time - keyframes[i].Time) / span;

                return Mathf.Lerp(
                    keyframes[i].Value,
                    keyframes[i + 1].Value,
                    localTime);
            }

            return keyframes[keyframes.Length - 1].Value;
        }

        private static float Ease(
            UIMotionEasing easing,
            float time)
        {
            switch (easing)
            {
                case UIMotionEasing.EaseInOut:
                    return time * time * (3f - 2f * time);

                case UIMotionEasing.Linear:
                default:
                    return time;
            }
        }

        private static void ApplyProperty(
            IUITransformCapability capability,
            UIMotionProperty property,
            float value)
        {
            switch (property)
            {
                case UIMotionProperty.Opacity:
                    capability.Opacity = value;
                    break;

                case UIMotionProperty.PositionX:
                {
                    var position = capability.Position;
                    position.x = value;
                    capability.Position = position;
                    break;
                }

                case UIMotionProperty.PositionY:
                {
                    var position = capability.Position;
                    position.y = value;
                    capability.Position = position;
                    break;
                }

                case UIMotionProperty.ScaleX:
                {
                    var scale = capability.Scale;
                    scale.x = value;
                    capability.Scale = scale;
                    break;
                }

                case UIMotionProperty.ScaleY:
                {
                    var scale = capability.Scale;
                    scale.y = value;
                    capability.Scale = scale;
                    break;
                }

                case UIMotionProperty.Rotation:
                    capability.Rotation = value;
                    break;
            }
        }
    }
}