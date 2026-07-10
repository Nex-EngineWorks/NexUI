using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// uGUI focus management via the EventSystem: selects the default element on trap and
    /// restores the previously-selected element on release (B6: focus lifecycle - a popup
    /// opened over a screen should hand focus back to whatever the screen had selected, not just
    /// clear it). Also applies a visible focus-ring (B3: basic keyboard-navigation support by
    /// default) so the selection isn't invisible out of the box - Unity's Selectable highlight
    /// alone is easy to miss, especially with a custom/transparent button graphic.
    /// </summary>
    public sealed class UGUIFocusAdapter : IUIFocusAdapter
    {
        private const float RingWidth = 2f;
        private static readonly Color RingColor = new Color(0.376f, 0.647f, 0.980f, 1f);

        // A stack, not a single slot, so nested traps (e.g. a confirm dialog opened over a
        // settings screen, itself trapped) unwind back through each previous selection in turn
        // rather than only remembering the immediately-preceding one.
        private readonly Stack<GameObject> _previousSelection = new Stack<GameObject>();

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
            {
                _previousSelection.Push(es.currentSelectedGameObject);
                es.SetSelectedGameObject(target);
                ApplyFocusRing(target);
            }
        }

        public void Release(IUISurface surface, bool restorePrevious)
        {
            var es = EventSystem.current;
            if (es != null)
                ClearFocusRing(es.currentSelectedGameObject);

            GameObject restore = null;
            if (_previousSelection.Count > 0)
            {
                restore = _previousSelection.Pop();
                // A previous selection can have been destroyed/deactivated while this screen was
                // trapped (e.g. the underlying screen closed too) - fall back to clearing instead
                // of selecting a dead object.
                if (restore != null && !restore.activeInHierarchy) restore = null;
            }

            es?.SetSelectedGameObject(restorePrevious ? restore : null);
            if (restorePrevious && restore != null)
                ApplyFocusRing(restore);
        }

        private static void ApplyFocusRing(GameObject target)
        {
            var outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = RingColor;
            outline.effectDistance = new Vector2(RingWidth, -RingWidth);
            outline.enabled = true;
        }

        private static void ClearFocusRing(GameObject target)
        {
            if (target == null) return;
            var outline = target.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }
    }
}
