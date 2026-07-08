using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Builds timelines for layout transitions (move / resize-by-scale) so an element can
    /// animate from a previous transform to a new one. Produces a compiled timeline the
    /// caller plays through an <see cref="IUIMotionPlayer"/>.
    /// </summary>
    public static class LayoutMotion
    {
        public static UIMotionTimeline Move(
            UnityEngine.Vector2 from, UnityEngine.Vector2 to,
            float duration, UIMotionEasing easing = UIMotionEasing.EaseInOut)
        {
            return new UIMotionTimeline
            {
                MotionId = "layout.move",
                Tracks = new[]
                {
                    Track(UIMotionProperty.PositionX, from.x, to.x, duration, easing),
                    Track(UIMotionProperty.PositionY, from.y, to.y, duration, easing),
                }
            };
        }

        private static UIMotionTrack Track(UIMotionProperty p, float from, float to, float dur, UIMotionEasing e)
            => new UIMotionTrack
            {
                Property = p,
                Easing = e,
                Duration = dur <= 0f ? 0.0001f : dur,
                Delay = 0f,
                Keyframes = new[] { new UIMotionKeyframe(0f, from), new UIMotionKeyframe(1f, to) }
            };
    }
}
