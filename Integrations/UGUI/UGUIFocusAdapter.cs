using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// uGUI focus management via the EventSystem: selects the default element on trap and
    /// clears selection on release.
    /// </summary>
    public sealed class UGUIFocusAdapter : IUIFocusAdapter
    {
        public UIRenderBackend Backend => UIRenderBackend.UGUI;

        public void Trap(IUISurface surface, string defaultElementId)
        {
            var es = EventSystem.current;
            if (es == null || surface == null) return;

            GameObject target = null;
            var handle = string.IsNullOrEmpty(defaultElementId) ? null : surface.TryFind(defaultElementId);
            if (handle != null && handle.Native is GameObject go)
            {
                target = go;
            }
            else if (surface.NativeRoot is GameObject root)
            {
                var selectable = root.GetComponentInChildren<Selectable>();
                if (selectable != null) target = selectable.gameObject;
            }

            if (target != null)
                es.SetSelectedGameObject(target);
        }

        public void Release(IUISurface surface, bool restorePrevious)
        {
            var es = EventSystem.current;
            if (es == null) return;
            es.SetSelectedGameObject(null);
        }
    }
}
