using System;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>All property tracks animating a single target element (by id) within a <see cref="UIMotionClip"/>.</summary>
    [Serializable]
    public sealed class UIMotionClipTrack
    {
        public string targetElementId;
        public UIMotionClipPropertyTrack[] propertyTracks = Array.Empty<UIMotionClipPropertyTrack>();
    }
}
