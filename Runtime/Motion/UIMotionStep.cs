using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Authoring unit: one property animating from <see cref="from"/> to <see cref="to"/>
    /// over a duration, with easing and an optional start delay. Compiled into a
    /// <see cref="UIMotionTrack"/> by the <see cref="MotionCompiler"/>.
    /// </summary>
    [Serializable]
    public struct UIMotionStep
    {
        public UIMotionProperty property;
        public float from;
        public float to;
        public float duration;
        public float delay;
        public UIMotionEasing easing;

        public static UIMotionStep Fade(float from, float to, float duration, float delay = 0f) =>
            new UIMotionStep
            {
                property = UIMotionProperty.Opacity,
                from = from, to = to, duration = duration, delay = delay,
                easing = UIMotionEasing.EaseInOut
            };
    }
}
