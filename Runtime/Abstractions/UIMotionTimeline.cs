using System;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Animatable transform properties supported by the built-in motion player.
    /// Kept in Abstractions so both the Motion module and the playback contract
    /// (<see cref="IUIMotionPlayer"/>) can reference it without a cyclic dependency.
    /// </summary>
    public enum UIMotionProperty
    {
        Opacity = 0,
        PositionX = 1,
        PositionY = 2,
        ScaleX = 3,
        ScaleY = 4,
        Rotation = 5
    }

    /// <summary>Easing functions supported by the built-in fallback player.</summary>
    public enum UIMotionEasing
    {
        Linear = 0,
        EaseInOut = 1
    }

    /// <summary>A single compiled keyframe: value at a normalized time [0..1] within its track.</summary>
    [Serializable]
    public struct UIMotionKeyframe
    {
        public float Time;
        public float Value;

        public UIMotionKeyframe(float time, float value)
        {
            Time = time;
            Value = value;
        }
    }

    /// <summary>A compiled track: one property animated over a duration with an easing.</summary>
    [Serializable]
    public sealed class UIMotionTrack
    {
        public UIMotionProperty Property;
        public UIMotionEasing Easing;
        public float Duration;
        public float Delay;
        public UIMotionKeyframe[] Keyframes;
    }

    /// <summary>
    /// Compiled, runtime-playable motion. Produced by the Motion module's compiler
    /// from authoring assets; consumed by any <see cref="IUIMotionPlayer"/>.
    /// </summary>
    [Serializable]
    public sealed class UIMotionTimeline
    {
        public string MotionId;
        public UIMotionTrack[] Tracks;

        public float TotalDuration
        {
            get
            {
                float max = 0f;
                if (Tracks == null) return 0f;
                foreach (var t in Tracks)
                {
                    if (t == null) continue;
                    float end = t.Delay + t.Duration;
                    if (end > max) max = end;
                }
                return max;
            }
        }

        public static readonly UIMotionTimeline Empty = new UIMotionTimeline
        {
            MotionId = string.Empty,
            Tracks = Array.Empty<UIMotionTrack>()
        };
    }
}
