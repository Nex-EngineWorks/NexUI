using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Resolves a backend-neutral handle to the uGUI object behind it.
    /// </summary>
    /// <remarks>
    /// <see cref="IUIElementHandle.Native"/> is documented as off-limits to Core and available to
    /// integrations, and anchoring is exactly the case it exists for: there is no capability for
    /// "where is this on screen", and inventing one would put a rect into a contract that UI Toolkit
    /// answers in completely different coordinates.
    /// </remarks>
    internal static class UGUIHandleRect
    {
        public static RectTransform Resolve(IUIElementHandle handle)
        {
            switch (handle?.Native)
            {
                case RectTransform rect: return rect;
                case GameObject go: return go.transform as RectTransform;
                case Component component: return component.transform as RectTransform;
                default: return null;
            }
        }
    }
}
