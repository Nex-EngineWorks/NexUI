using System;
using emiteat.NexUI.Compiled;
using UnityEngine;
using UnityEngine.EventSystems;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Turns uGUI event-system callbacks into interaction triggers.
    /// </summary>
    /// <remarks>
    /// A purpose-built relay rather than <see cref="EventTrigger"/>. EventTrigger implements every
    /// handler interface whether or not the screen uses them, which makes the object a raycast
    /// participant for events nobody authored, and - worse - its presence changes how other
    /// components on the same object receive input. This relay implements only what it forwards
    /// and is attached only to nodes whose triggers were actually authored.
    ///
    /// Long press and double click are measured here rather than by the interaction engine because
    /// they are properties of the input device, not of the rule: the engine is a pure evaluator
    /// with no notion of frames, and giving it one to serve two triggers would make every screen
    /// pay for a per-frame update.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class NXInteractionRelay : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISubmitHandler, ICancelHandler,
        IDropHandler
    {
        /// <summary>How long a press must be held to count as a long press.</summary>
        public float LongPressSeconds = 0.5f;

        /// <summary>How close together two clicks must be to count as a double click.</summary>
        public float DoubleClickSeconds = 0.3f;

        /// <summary>Raised for each trigger this node authored. Never raised for others.</summary>
        public event Action<NexTrigger> Triggered;

        /// <summary>Bitmask of the triggers worth forwarding, so unauthored ones cost nothing.</summary>
        private int _wanted;

        private bool _pressed;
        private float _pressedAt;
        private bool _longPressSent;
        private float _lastClickAt = float.NegativeInfinity;

        /// <summary>Declares a trigger this node listens for.</summary>
        public void Want(NexTrigger trigger) => _wanted |= 1 << (int)trigger;

        public bool Wants(NexTrigger trigger) => (_wanted & (1 << (int)trigger)) != 0;

        /// <summary>Whether anything at all needs the per-frame press timer.</summary>
        private bool NeedsTimer => Wants(NexTrigger.OnLongPress);

        private void Raise(NexTrigger trigger)
        {
            if (Wants(trigger)) Triggered?.Invoke(trigger);
        }

        public void OnPointerEnter(PointerEventData eventData) => Raise(NexTrigger.OnPointerEnter);

        public void OnPointerExit(PointerEventData eventData)
        {
            // A pointer leaving cancels the press it started: holding down, dragging off and
            // releasing elsewhere is how a user takes back a press, and it must not arrive as a
            // long press half a second later.
            _pressed = false;
            _longPressSent = false;
            Raise(NexTrigger.OnPointerExit);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _pressedAt = UnityEngine.Time.unscaledTime;
            _longPressSent = false;
            Raise(NexTrigger.OnPointerDown);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var wasPressed = _pressed;
            _pressed = false;
            Raise(NexTrigger.OnPointerUp);

            if (!wasPressed || !Wants(NexTrigger.OnDoubleClick)) return;

            // A long press already consumed this press, so releasing it is not also a click.
            if (_longPressSent) { _longPressSent = false; return; }

            var now = UnityEngine.Time.unscaledTime;
            if (now - _lastClickAt <= DoubleClickSeconds)
            {
                Raise(NexTrigger.OnDoubleClick);

                // Reset rather than keep the timestamp, so a third click starts a new pair instead
                // of making every click after the second one a double.
                _lastClickAt = float.NegativeInfinity;
            }
            else
            {
                _lastClickAt = now;
            }
        }

        public void OnSubmit(BaseEventData eventData) => Raise(NexTrigger.OnSubmit);

        public void OnCancel(BaseEventData eventData) => Raise(NexTrigger.OnCancel);

        /// <summary>
        /// Receives a drop. Safe to have on every relay - unlike the drag handlers, implementing
        /// this does not make the object a candidate to be dragged.
        /// </summary>
        public void OnDrop(PointerEventData eventData) => Raise(NexTrigger.OnDrop);

        /// <summary>Cancels an in-flight press, for when a drag takes the gesture over.</summary>
        /// <remarks>
        /// Holding still and holding while moving are different gestures: one press must not
        /// arrive as both a long press and a drag.
        /// </remarks>
        internal void CancelPress()
        {
            _pressed = false;
            _longPressSent = false;
        }

        private void Update()
        {
            if (!_pressed || _longPressSent || !NeedsTimer) return;

            // Unscaled: a paused game still has a responsive UI, and a long press that needs the
            // timescale running would stop working exactly on the pause menu that needs it.
            if (UnityEngine.Time.unscaledTime - _pressedAt < LongPressSeconds) return;

            _longPressSent = true;
            Raise(NexTrigger.OnLongPress);
        }

        private void OnDisable()
        {
            // Being hidden mid-press must not leave a press that completes on re-enable.
            _pressed = false;
            _longPressSent = false;
        }
    }
}
