using System;
using UnityEngine;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// A multi-element, multi-property, keyframe-based UI animation asset — authored via the
    /// Motion Clip Editor (Designer) and played at runtime through <see cref="IUIMotionClipPlayer"/>.
    /// Parallel to (and independent of) the existing step-based <c>UIMotionPreset</c>/<c>UIMotionGraph</c>
    /// system in <c>emiteat.NexUI.Motion</c>; that system is unchanged by this one.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Motion Clip", fileName = "NewMotionClip")]
    public sealed class UIMotionClip : ScriptableObject
    {
        public string clipName = "NewMotionClip";
        public float duration = 1f;
        public bool loop;
        public UIMotionClipTrack[] tracks = Array.Empty<UIMotionClipTrack>();
    }
}
