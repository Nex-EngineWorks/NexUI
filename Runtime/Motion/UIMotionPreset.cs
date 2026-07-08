using System;
using UnityEngine;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Authoring asset: the source of truth for a motion. Holds variants and/or a graph.
    /// A screen definition references this asset (as a plain UnityEngine.Object) and the
    /// runtime compiles it to a <c>UIMotionTimeline</c> via <see cref="MotionCompiler"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Motion Preset", fileName = "NewMotionPreset")]
    public sealed class UIMotionPreset : ScriptableObject
    {
        public string motionId;
        public UIMotionVariant[] variants = Array.Empty<UIMotionVariant>();
        public UIMotionGraph graph = new UIMotionGraph();

        /// <summary>Name of the variant compiled by default when none is specified.</summary>
        public string defaultVariant = "default";
    }
}
