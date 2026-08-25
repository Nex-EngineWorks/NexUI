using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// A button that must be held before it fires, reporting progress while the player holds - the
    /// standard treatment for destructive actions ("hold to delete") and revive prompts. uGUI's Button
    /// only knows click.
    /// </summary>
    [AddComponentMenu("NexUI/Interaction/NX Hold Button")]
    public sealed class NXHoldButton : Button
    {
        [SerializeField, Tooltip("Seconds the button must be held. 0 behaves like a normal Button.")]
        private float m_HoldSeconds = 1f;
        [SerializeField, Tooltip("Cancel the hold when the pointer leaves the button.")]
        private bool m_CancelOnExit = true;

        [SerializeField] private UnityEvent<float> m_OnHoldProgress = new UnityEvent<float>();
        [SerializeField] private UnityEvent m_OnHoldComplete = new UnityEvent();

        private bool _holding;
        private float _elapsed;
        private bool _completed;

        public UnityEvent<float> OnHoldProgress => m_OnHoldProgress;
        public UnityEvent OnHoldComplete => m_OnHoldComplete;
        public float Progress => m_HoldSeconds <= 0f ? 0f : Mathf.Clamp01(_elapsed / m_HoldSeconds);

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (!IsActive() || !IsInteractable()) return;
            _holding = true;
            _completed = false;
            _elapsed = 0f;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            Release();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (m_CancelOnExit) Release();
        }

        /// <summary>A completed hold already fired; swallow the click so the action does not run twice.</summary>
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (m_HoldSeconds > 0f) return;
            base.OnPointerClick(eventData);
        }

        private void Release()
        {
            if (!_holding) return;
            _holding = false;
            _elapsed = 0f;
            if (!_completed) m_OnHoldProgress.Invoke(0f);
        }

        private void Update()
        {
            if (!_holding || _completed || m_HoldSeconds <= 0f) return;

            _elapsed += UnityTime.unscaledDeltaTime;
            m_OnHoldProgress.Invoke(Progress);
            if (_elapsed < m_HoldSeconds) return;

            _completed = true;
            _holding = false;
            m_OnHoldComplete.Invoke();
            onClick.Invoke();
        }
    }
}
