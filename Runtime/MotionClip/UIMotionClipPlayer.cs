using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// Default motion clip player.
    /// Evaluates every motion track using unscaled Unity time.
    /// </summary>
    public sealed class UIMotionClipPlayer : IUIMotionClipPlayer
    {
        private CancellationTokenSource _cts;

        public async Task PlayAsync(
            IUISurface surface,
            UIMotionClip clip,
            CancellationToken cancellationToken = default)
        {
            if (surface == null ||
                clip == null ||
                clip.tracks == null ||
                clip.tracks.Length == 0)
            {
                return;
            }

            Stop();

            var cts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            _cts = cts;

            try
            {
                var rangeStart = 0f;
                var rangeEnd = clip.duration;
                var firstPass = true;

                do
                {
                    var elapsed = rangeStart;

                    Evaluate(
                        surface,
                        clip,
                        elapsed);

                    while (elapsed < rangeEnd)
                    {
                        await Task.Yield();

                        if (cts.IsCancellationRequested)
                        {
                            break;
                        }

                        elapsed += UnityTime.unscaledDeltaTime;

                        Evaluate(
                            surface,
                            clip,
                            Mathf.Min(elapsed, rangeEnd));
                    }

                    if (cts.IsCancellationRequested)
                    {
                        break;
                    }

                    Evaluate(
                        surface,
                        clip,
                        rangeEnd);

                    // 첫 전체 재생 이후에는 Work Area 구간만 반복한다.
                    if (firstPass &&
                        clip.useWorkArea &&
                        clip.workAreaEnd > clip.workAreaStart)
                    {
                        rangeStart = clip.workAreaStart;
                        rangeEnd = clip.workAreaEnd;
                    }

                    firstPass = false;
                }
                while (clip.loop && !cts.IsCancellationRequested);
            }
            finally
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _cts = null;
                }

                cts.Dispose();
            }
        }

        public void Stop()
        {
            var cts = _cts;

            if (cts == null)
            {
                return;
            }

            _cts = null;

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }

            // 여기서 Dispose하지 않는다.
            // PlayAsync의 finally에서 Dispose한다.
        }

        public void Evaluate(
            IUISurface surface,
            UIMotionClip clip,
            float time)
        {
            if (surface == null || clip?.tracks == null)
            {
                return;
            }

            foreach (var track in clip.tracks)
            {
                if (track?.propertyTracks == null)
                {
                    continue;
                }

                var target =
                    UIMotionClipTargetResolver.Resolve(
                        surface,
                        track.targetElementId);

                if (target == null)
                {
                    continue;
                }

                foreach (var propertyTrack in track.propertyTracks)
                {
                    if (propertyTrack == null)
                    {
                        continue;
                    }

                    var value =
                        UIMotionClipEvaluator.Evaluate(
                            propertyTrack,
                            time);

                    if (value.HasValue)
                    {
                        Apply(
                            target,
                            propertyTrack.propertyType,
                            value.Value);
                    }
                }
            }
        }

        private static void Apply(
            IUIElementHandle target,
            UIMotionClipPropertyType property,
            UIMotionClipValue value)
        {
            switch (property)
            {
                case UIMotionClipPropertyType.AnchoredPosition:
                case UIMotionClipPropertyType.LocalPosition:
                {
                    var capability =
                        target.As<IUITransformCapability>();

                    if (capability != null)
                    {
                        capability.Position =
                            value.vector2Value;
                    }

                    break;
                }

                case UIMotionClipPropertyType.LocalRotationZ:
                {
                    var capability =
                        target.As<IUITransformCapability>();

                    if (capability != null)
                    {
                        capability.Rotation =
                            value.floatValue;
                    }

                    break;
                }

                case UIMotionClipPropertyType.LocalScale:
                {
                    var capability =
                        target.As<IUITransformCapability>();

                    if (capability != null)
                    {
                        capability.Scale =
                            value.vector3Value;
                    }

                    break;
                }

                case UIMotionClipPropertyType.SizeDelta:
                {
                    var capability =
                        target.As<IUISizeCapability>();

                    if (capability != null)
                    {
                        capability.SizeDelta =
                            value.vector2Value;
                    }

                    break;
                }

                case UIMotionClipPropertyType.CanvasGroupAlpha:
                {
                    var capability =
                        target.As<IUITransformCapability>();

                    if (capability != null)
                    {
                        capability.Opacity =
                            value.floatValue;
                    }

                    break;
                }
            }
        }
    }
}