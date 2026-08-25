using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
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
}
