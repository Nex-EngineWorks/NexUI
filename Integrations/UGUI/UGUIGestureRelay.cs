using System;
using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>Exposes uGUI EventSystem pointer/focus callbacks as NexUI capabilities.</summary>
    [AddComponentMenu("")]
    public sealed class UGUIGestureRelay : MonoBehaviour, IUIPointerCapability, IUIFocusCapability,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        public event Action PointerEntered;
        public event Action PointerExited;
        public event Action PointerDown;
        public event Action PointerUp;
        public event Action Focused;
        public event Action Blurred;

        public bool HasFocus => EventSystem.current != null &&
                                EventSystem.current.currentSelectedGameObject == gameObject;

        public void OnPointerEnter(PointerEventData eventData) => PointerEntered?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => PointerExited?.Invoke();
        public void OnPointerDown(PointerEventData eventData) => PointerDown?.Invoke();
        public void OnPointerUp(PointerEventData eventData) => PointerUp?.Invoke();
        public void OnSelect(BaseEventData eventData) => Focused?.Invoke();
        public void OnDeselect(BaseEventData eventData) => Blurred?.Invoke();
    }
}
