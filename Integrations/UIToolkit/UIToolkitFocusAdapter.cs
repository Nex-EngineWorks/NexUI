using emiteat.NexUI.Abstractions;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// UI Toolkit focus management. Focuses the default element on trap and clears focus
    /// on release. A full focus-cycle trap can be layered on later via keydown handlers.
    /// </summary>
    public sealed class UIToolkitFocusAdapter : IUIFocusAdapter
    {
        public UIRenderBackend Backend => UIRenderBackend.UIToolkit;

        public void Trap(IUISurface surface, string defaultElementId)
        {
            if (surface == null) return;

            var handle = string.IsNullOrEmpty(defaultElementId) ? null : surface.TryFind(defaultElementId);
            if (handle != null && handle.Native is VisualElement ve)
            {
                ve.Focus();
            }
            else if (surface.NativeRoot is VisualElement root)
            {
                root.Focus();
            }
        }

        public void Release(IUISurface surface, bool restorePrevious)
        {
            if (surface?.NativeRoot is VisualElement root)
                root.Blur();
        }
    }
}
