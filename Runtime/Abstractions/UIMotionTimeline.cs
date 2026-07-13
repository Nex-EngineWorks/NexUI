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

    /// <summary>
    /// Easing functions. <see cref="Linear"/>/<see cref="EaseInOut"/> are the original two, evaluated
    /// by every consumer (<c>BuiltInMotionPlayer</c>, <c>UIMotionClipEvaluator</c>). Everything below
    /// them is the standard Penner easing set added for the Motion Clip Editor's Easing Browser
    /// (<c>emiteat.NexUI.MotionClip.UIMotionClipEvaluator.Ease</c> is the only place that evaluates
    /// these; older consumers fall back to linear via their existing <c>default:</c> case, so this is
    /// a purely additive, migration-safe change - existing serialized clips/presets are unaffected).
    /// Numeric values are fixed and must never be renumbered (they're what gets serialized).
    /// </summary>
    public enum UIMotionEasing
    {
        Linear = 0,
        EaseInOut = 1,

        EaseInQuad = 2,
        EaseOutQuad = 3,
        EaseInOutQuad = 4,

        EaseInCubic = 5,
        EaseOutCubic = 6,
        EaseInOutCubic = 7,

        EaseInQuart = 8,
        EaseOutQuart = 9,
        EaseInOutQuart = 10,

        EaseInQuint = 11,
        EaseOutQuint = 12,
        EaseInOutQuint = 13,

        EaseInSine = 14,
        EaseOutSine = 15,
        EaseInOutSine = 16,

        EaseInExpo = 17,
        EaseOutExpo = 18,
        EaseInOutExpo = 19,

        EaseInCirc = 20,
        EaseOutCirc = 21,
        EaseInOutCirc = 22,

        EaseInBack = 23,
        EaseOutBack = 24,
        EaseInOutBack = 25,

        EaseInElastic = 26,
        EaseOutElastic = 27,
        EaseInOutElastic = 28,

        EaseInBounce = 29,
        EaseOutBounce = 30,
        EaseInOutBounce = 31
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
