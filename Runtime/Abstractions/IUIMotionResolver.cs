namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Resolves a serialized motion authoring asset into a compiled
    /// <see cref="UIMotionTimeline"/>. This abstraction lets Core request motion
    /// playback from a screen definition (which references motion assets as plain
    /// <see cref="UnityEngine.Object"/>) without ever depending on the Motion module.
    /// </summary>
    public interface IUIMotionResolver
    {
        /// <summary>
        /// Compile / look up a timeline for the given authoring asset.
        /// Returns <see cref="UIMotionTimeline.Empty"/> when the asset is null or unsupported.
        /// </summary>
        UIMotionTimeline Resolve(UnityEngine.Object motionAsset);
    }
}
