using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// Resolves a <see cref="UIMotionClipTrack.targetElementId"/> against a live
    /// <see cref="IUISurface"/>. Used identically by the runtime player and the editor preview
    /// controller, since both operate against the same <see cref="IUISurface"/> abstraction
    /// (a live screen surface at runtime, the Designer's preview surface in-editor).
    /// </summary>
    public static class UIMotionClipTargetResolver
    {
        public static IUIElementHandle Resolve(IUISurface surface, string targetElementId)
        {
            if (surface == null) return null;
            return string.IsNullOrEmpty(targetElementId) ? surface.RootHandle : surface.TryFind(targetElementId);
        }
    }
}
