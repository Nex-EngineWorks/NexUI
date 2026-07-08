#if NEXUI_HAS_DOTWEEN
using emiteat.NexUI.Core;
using emiteat.NexUI.Motion;

namespace emiteat.NexUI.Integrations.DOTween
{
    /// <summary>Swaps a manager's motion player for the DOTween implementation.</summary>
    public static class DOTweenIntegrationBootstrap
    {
        public static void Use(UIManager manager)
        {
            if (manager == null) return;
            manager.MotionPlayer = new DOTweenMotionPlayer();
            manager.MotionResolver ??= new MotionResolver();
        }
    }
}
#endif
