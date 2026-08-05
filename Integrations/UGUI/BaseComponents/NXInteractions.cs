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

    /// <summary>
    /// Reports swipes over a region: direction, distance and velocity. Card decks, page switchers and
    /// dismissible panels all need this, and uGUI offers only raw drag callbacks.
    /// </summary>
    [AddComponentMenu("NexUI/Interaction/NX Swipe Area")]
    public sealed class NXSwipeArea : UIBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum SwipeDirection { None, Left, Right, Up, Down }

        [SerializeField, Tooltip("Minimum distance in pixels before a drag counts as a swipe.")]
        private float m_Threshold = 60f;
        [SerializeField, Tooltip("Restrict recognition to one axis.")]
        private bool m_HorizontalOnly;
        [SerializeField] private bool m_VerticalOnly;

        [SerializeField] private UnityEvent<int> m_OnSwipe = new UnityEvent<int>();

        private Vector2 _start;
        private bool _dragging;

        /// <summary>Fires with the <see cref="SwipeDirection"/> as an int so it is assignable in the Inspector.</summary>
        public UnityEvent<int> OnSwipe => m_OnSwipe;
        public SwipeDirection LastSwipe { get; private set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _start = eventData.position;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            var delta = eventData.position - _start;
            if (delta.magnitude < m_Threshold) { LastSwipe = SwipeDirection.None; return; }

            var horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            if (m_HorizontalOnly) horizontal = true;
            if (m_VerticalOnly) horizontal = false;

            LastSwipe = horizontal
                ? delta.x > 0f ? SwipeDirection.Right : SwipeDirection.Left
                : delta.y > 0f ? SwipeDirection.Up : SwipeDirection.Down;

            m_OnSwipe.Invoke((int)LastSwipe);
        }
    }

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
