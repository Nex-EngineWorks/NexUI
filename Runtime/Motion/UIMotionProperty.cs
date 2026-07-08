using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Motion-domain helpers for <see cref="UIMotionProperty"/>.
    ///
    /// NOTE: the <c>UIMotionProperty</c> / <c>UIMotionEasing</c> enums and the compiled
    /// timeline types live in <c>emiteat.NexUI.Abstractions</c> (not here) so that the
    /// playback contract <c>IUIMotionPlayer</c> can reference them without creating a
    /// cycle back into the Motion module. This file provides authoring-side helpers.
    /// </summary>
    public static class UIMotionProperties
    {
        /// <summary>The resting / identity value for a property (used when a step omits 'from').</summary>
        public static float DefaultValue(UIMotionProperty property)
        {
            switch (property)
            {
                case UIMotionProperty.Opacity:
                case UIMotionProperty.ScaleX:
                case UIMotionProperty.ScaleY:
                    return 1f;
                case UIMotionProperty.PositionX:
                case UIMotionProperty.PositionY:
                case UIMotionProperty.Rotation:
                default:
                    return 0f;
            }
        }

        public static bool IsPosition(UIMotionProperty p)
            => p == UIMotionProperty.PositionX || p == UIMotionProperty.PositionY;

        public static bool IsScale(UIMotionProperty p)
            => p == UIMotionProperty.ScaleX || p == UIMotionProperty.ScaleY;
    }
}
