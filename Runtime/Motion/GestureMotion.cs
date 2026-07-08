using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Lightweight, immediate (non-timeline) gesture feedback such as press-scale or
    /// hover-lift. Applies directly through <see cref="IUITransformCapability"/>.
    /// </summary>
    public static class GestureMotion
    {
        public static void PressDown(IUIElementHandle target, float scale = 0.95f)
        {
            var cap = target?.As<IUITransformCapability>();
            if (cap == null) return;
            cap.Scale = new UnityEngine.Vector3(scale, scale, 1f);
        }

        public static void PressUp(IUIElementHandle target)
        {
            var cap = target?.As<IUITransformCapability>();
            if (cap == null) return;
            cap.Scale = UnityEngine.Vector3.one;
        }

        public static void HoverLift(IUIElementHandle target, float offsetY = 4f)
        {
            var cap = target?.As<IUITransformCapability>();
            if (cap == null) return;
            var p = cap.Position; p.y += offsetY; cap.Position = p;
        }
    }
}
