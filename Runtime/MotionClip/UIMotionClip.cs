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

        /// <summary>Display/snap rate for the Motion Clip Editor timeline (frames per second). Does not affect playback, which is time-based.</summary>
        public int fps = 30;

        /// <summary>
        /// When true and <see cref="loop"/> is also true, only the first pass plays the full
        /// 0..<see cref="duration"/> range; every pass after that loops <see cref="workAreaStart"/>..
        /// <see cref="workAreaEnd"/> instead - the common "intro, then looping middle segment" idle-
        /// animation pattern. Has no effect when <see cref="loop"/> is false.
        /// </summary>
        public bool useWorkArea;
        public float workAreaStart;
        public float workAreaEnd = 1f;

        /// <summary>Named authoring-time markers on the Motion Clip Editor timeline (e.g. "Impact", "Loop Point"). Purely an authoring aid - never evaluated during playback.</summary>
        public UIMotionClipMarker[] markers = Array.Empty<UIMotionClipMarker>();

        public UIMotionClipTrack[] tracks = Array.Empty<UIMotionClipTrack>();
    }

    /// <summary>A named point in time on a <see cref="UIMotionClip"/>'s timeline, authored in the Motion Clip Editor.</summary>
    [Serializable]
    public struct UIMotionClipMarker
    {
        public string name;
        public float time;

        public UIMotionClipMarker(string name, float time)
        {
            this.name = name;
            this.time = time;
        }
    }
}
