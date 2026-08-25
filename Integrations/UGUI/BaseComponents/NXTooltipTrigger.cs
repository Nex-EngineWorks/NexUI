using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Turns any element into a tooltip trigger with the delays a real tooltip needs (show delay, hide
    /// delay, follow the pointer). Unity has no tooltip system for runtime UI at all.
    /// </summary>
    [AddComponentMenu("NexUI/Interaction/NX Tooltip Trigger")]
    public sealed class NXTooltipTrigger : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, TextArea] private string m_Text = "";
        [SerializeField] private float m_ShowDelay = 0.4f;
        [SerializeField] private float m_HideDelay = 0.1f;
        [SerializeField, Tooltip("Element shown as the tooltip. Left empty, the trigger only raises its events.")]
        private RectTransform m_Tooltip;
        [SerializeField] private bool m_FollowPointer;

        [SerializeField] private UnityEvent<string> m_OnShow = new UnityEvent<string>();
        [SerializeField] private UnityEvent m_OnHide = new UnityEvent();

        private float _timer;
        private bool _hovered;
        private bool _shown;

        public string Text
        {
            get => m_Text;
            set => m_Text = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            _timer = m_ShowDelay;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _timer = m_HideDelay;
        }

        private void Update()
        {
            if (_timer > 0f)
            {
                _timer -= UnityTime.unscaledDeltaTime;
                if (_timer <= 0f)
                {
                    if (_hovered && !_shown) Show();
                    else if (!_hovered && _shown) Hide();
                }
            }

            if (_shown && m_FollowPointer && m_Tooltip != null)
                m_Tooltip.position = Input.mousePosition;
        }

        private void Show()
        {
            _shown = true;
            if (m_Tooltip != null) m_Tooltip.gameObject.SetActive(true);
            m_OnShow.Invoke(m_Text);
        }

        private void Hide()
        {
            _shown = false;
            if (m_Tooltip != null) m_Tooltip.gameObject.SetActive(false);
            m_OnHide.Invoke();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_shown) Hide();
        }
    }
}
