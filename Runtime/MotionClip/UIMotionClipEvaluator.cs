using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// Evaluates a <see cref="UIMotionClipPropertyTrack"/> at a point in time.
    /// Rules: 0 keyframes -> no value (null); 1 keyframe -> that value; time before the first
    /// keyframe -> first value; time after the last -> last value; otherwise interpolate between
    /// the bracketing pair using the outgoing keyframe's curve (if set) or easing.
    /// </summary>
    public static class UIMotionClipEvaluator
    {
        public static UIMotionClipValue? Evaluate(UIMotionClipPropertyTrack track, float time)
        {
            if (track?.keyframes == null || track.keyframes.Length == 0)
                return null;

            var keyframes = track.keyframes;
            if (keyframes.Length == 1)
                return keyframes[0].value;

            if (time <= keyframes[0].time)
                return keyframes[0].value;

            var last = keyframes[keyframes.Length - 1];
            if (time >= last.time)
                return last.value;

            for (var i = 0; i < keyframes.Length - 1; i++)
            {
                var a = keyframes[i];
                var b = keyframes[i + 1];
                if (time < a.time || time > b.time) continue;

                var span = b.time - a.time;
                var t = span > 0f ? (time - a.time) / span : 0f;
                var eased = a.curve != null ? a.curve.Evaluate(t) : Ease(a.easing, t);
                return UIMotionClipValue.Lerp(a.value, b.value, eased);
            }

            return last.value;
        }

        /// <summary>
        /// Standard Penner easing set. Public so the Designer's Easing Browser can render exactly
        /// the curve that will actually play, rather than a separately-maintained approximation.
        /// </summary>
        public static float Ease(UIMotionEasing easing, float t)
        {
            switch (easing)
            {
                case UIMotionEasing.EaseInOut:
                    return t * t * (3f - 2f * t);

                case UIMotionEasing.EaseInQuad: return t * t;
                case UIMotionEasing.EaseOutQuad: return 1f - (1f - t) * (1f - t);
                case UIMotionEasing.EaseInOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                case UIMotionEasing.EaseInCubic: return t * t * t;
                case UIMotionEasing.EaseOutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case UIMotionEasing.EaseInOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

                case UIMotionEasing.EaseInQuart: return t * t * t * t;
                case UIMotionEasing.EaseOutQuart: return 1f - Mathf.Pow(1f - t, 4f);
                case UIMotionEasing.EaseInOutQuart:
                    return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;

                case UIMotionEasing.EaseInQuint: return t * t * t * t * t;
                case UIMotionEasing.EaseOutQuint: return 1f - Mathf.Pow(1f - t, 5f);
                case UIMotionEasing.EaseInOutQuint:
                    return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;

                case UIMotionEasing.EaseInSine: return 1f - Mathf.Cos(t * Mathf.PI / 2f);
                case UIMotionEasing.EaseOutSine: return Mathf.Sin(t * Mathf.PI / 2f);
                case UIMotionEasing.EaseInOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

                case UIMotionEasing.EaseInExpo:
                    return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case UIMotionEasing.EaseOutExpo:
                    return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case UIMotionEasing.EaseInOutExpo:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) / 2f : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;

                case UIMotionEasing.EaseInCirc: return 1f - Mathf.Sqrt(1f - t * t);
                case UIMotionEasing.EaseOutCirc: return Mathf.Sqrt(1f - (t - 1f) * (t - 1f));
                case UIMotionEasing.EaseInOutCirc:
                    return t < 0.5f
                        ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) / 2f
                        : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) / 2f;

                case UIMotionEasing.EaseInBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return c3 * t * t * t - c1 * t * t;
                }
                case UIMotionEasing.EaseOutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                }
                case UIMotionEasing.EaseInOutBack:
                {
                    const float c1 = 1.70158f, c2 = c1 * 1.525f;
                    return t < 0.5f
                        ? Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2) / 2f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
                }

                case UIMotionEasing.EaseInElastic:
                {
                    const float c4 = 2f * Mathf.PI / 3f;
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * c4);
                }
                case UIMotionEasing.EaseOutElastic:
                {
                    const float c4 = 2f * Mathf.PI / 3f;
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                case UIMotionEasing.EaseInOutElastic:
                {
                    const float c5 = 2f * Mathf.PI / 4.5f;
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) / 2f
                        : Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * c5) / 2f + 1f;
                }

                case UIMotionEasing.EaseInBounce: return 1f - EaseOutBounce(1f - t);
                case UIMotionEasing.EaseOutBounce: return EaseOutBounce(t);
                case UIMotionEasing.EaseInOutBounce:
                    return t < 0.5f
                        ? (1f - EaseOutBounce(1f - 2f * t)) / 2f
                        : (1f + EaseOutBounce(2f * t - 1f)) / 2f;

                default:
                    return t;
            }
        }

        private static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
