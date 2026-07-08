#if NEXUI_HAS_DOTWEEN
using DG.Tweening;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Integrations.DOTween
{
    /// <summary>Helpers mapping NexUI motion enums onto DOTween.</summary>
    public static class DOTweenMotionExtensions
    {
        public static Ease ToDOTweenEase(this UIMotionEasing easing)
        {
            switch (easing)
            {
                case UIMotionEasing.EaseInOut: return Ease.InOutQuad;
                case UIMotionEasing.Linear:
                default: return Ease.Linear;
            }
        }
    }
}
#endif
