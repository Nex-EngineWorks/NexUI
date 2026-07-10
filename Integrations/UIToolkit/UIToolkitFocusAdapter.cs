using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// UI Toolkit focus management. Focuses the default element on trap and restores the
    /// previously-focused element on release (B6: focus lifecycle - a popup opened over a screen
    /// should hand focus back to whatever the screen had focused, not just clear it).
    /// </summary>
    public sealed class UIToolkitFocusAdapter : IUIFocusAdapter
    {
        // A stack so nested traps (e.g. a confirm dialog over a trapped settings screen) unwind
        // back through each previous focus in turn.
        private readonly Stack<Focusable> _previousFocus = new Stack<Focusable>();

        public UIRenderBackend Backend => UIRenderBackend.UIToolkit;

        public void Trap(IUISurface surface, string defaultElementId)
        {
            if (surface == null) return;

            VisualElement target = null;
            var handle = string.IsNullOrEmpty(defaultElementId) ? null : surface.TryFind(defaultElementId);
            if (handle != null && handle.Native is VisualElement ve)
                target = ve;
            else if (surface.NativeRoot is VisualElement root)
                target = root;

            if (target == null) return;

            _previousFocus.Push(target.focusController?.focusedElement);
            target.Focus();
        }

        public void Release(IUISurface surface, bool restorePrevious)
        {
            if (surface?.NativeRoot is not VisualElement root) return;

            Focusable restore = null;
            if (_previousFocus.Count > 0)
            {
                restore = _previousFocus.Pop();
                // A previously-focused element can have been removed from the panel while this
                // surface was trapped - fall back to clearing instead of focusing a dead element.
                if (restore is VisualElement restoreVe && restoreVe.panel == null) restore = null;
            }

            root.Blur();
            if (restorePrevious) restore?.Focus();
        }
    }
}
