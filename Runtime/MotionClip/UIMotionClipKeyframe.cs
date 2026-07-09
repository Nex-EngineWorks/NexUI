using System;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// One keyframe in a <see cref="UIMotionClipPropertyTrack"/>. When <see cref="curve"/> is
    /// non-null it overrides <see cref="easing"/> for interpolation out of this keyframe
    /// (evaluated 0..1 across the segment to the next keyframe).
    /// </summary>
    [Serializable]
    public struct UIMotionClipKeyframe
    {
        public float time;
        public UIMotionClipValue value;
        public UIMotionEasing easing;
        public AnimationCurve curve;

        public UIMotionClipKeyframe(float time, UIMotionClipValue value, UIMotionEasing easing = UIMotionEasing.Linear, AnimationCurve curve = null)
        {
            this.time = time;
            this.value = value;
            this.easing = easing;
            this.curve = curve;
        }
    }
}
