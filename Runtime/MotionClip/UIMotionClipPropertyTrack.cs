using System;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>Keyframes for a single <see cref="UIMotionClipPropertyType"/> on one target element.</summary>
    [Serializable]
    public sealed class UIMotionClipPropertyTrack
    {
        public UIMotionClipPropertyType propertyType;
        public UIMotionClipKeyframe[] keyframes = Array.Empty<UIMotionClipKeyframe>();
    }
}
