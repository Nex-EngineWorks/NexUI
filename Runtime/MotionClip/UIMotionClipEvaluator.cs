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

        private static float Ease(UIMotionEasing easing, float t)
        {
            switch (easing)
            {
                case UIMotionEasing.EaseInOut:
                    return t * t * (3f - 2f * t);
                default:
                    return t;
            }
        }
    }
}
