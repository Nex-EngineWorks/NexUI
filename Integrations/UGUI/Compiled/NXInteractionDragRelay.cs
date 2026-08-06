using System;
using emiteat.NexUI.Compiled;
using UnityEngine;
using UnityEngine.EventSystems;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>How a dragged element shows that it is being dragged.</summary>
    public enum NexDragVisual
    {
        /// <summary>Nothing moves. The rules do whatever feedback there is.</summary>
        None = 0,

        /// <summary>The element itself follows the pointer, and returns if the drop is refused.</summary>
        MoveSelf = 1,

        /// <summary>
        /// A translucent copy follows the pointer while the element stays put.
        /// </summary>
        /// <remarks>
        /// What an inventory wants: the source slot keeps showing its item until the move is
        /// actually accepted, so a refused drop needs no undo - nothing moved in the first place.
        /// </remarks>
        Ghost = 2
    }

    /// <summary>
    /// Turns uGUI drag callbacks into interaction triggers, on nodes authored as drag sources.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="NXInteractionRelay"/> for a reason that is easy to get wrong.
    /// uGUI picks the drag target by searching up the hierarchy for the first object implementing
    /// <see cref="IBeginDragHandler"/>, and it does that <em>before</em> any of this code runs -
    /// so an object merely implementing the interface has already claimed the gesture, and
    /// returning early from the handler does not give it back. Had the drag handlers lived on the
    /// shared relay, a panel that authored nothing but a hover would silently stop every drag that
    /// should have scrolled the list underneath it.
    ///
    /// Keeping them here means the interface exists only where a drag was actually authored, and
    /// scrolling keeps working everywhere else.
    ///
    /// <see cref="IDropHandler"/> is deliberately <em>not</em> here: receiving a drop does not make
    /// an object draggable, so it stays on the shared relay where a drop target needs no second
    /// component.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class NXInteractionDragRelay : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Raised for each drag trigger this node authored.</summary>
        public event Action<NexTrigger> Triggered;

        /// <summary>
        /// Raised true when a drag starts here and false when it ends, so the engine can publish
        /// which element is being dragged.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Triggered"/> because it fires even when this node authored no
        /// drag rule of its own: the element that reacts to a drop is the target, and the target
        /// can only identify what it caught if the source was published.
        /// </remarks>
        public event Action<bool> DragSourceChanged;

        /// <summary>What follows the pointer while this element is dragged.</summary>
        public NexDragVisual Visual = NexDragVisual.None;

        /// <summary>Opacity of the ghost copy, when <see cref="Visual"/> is Ghost.</summary>
        public float GhostOpacity = 0.7f;

        /// <summary>
        /// Whether a <see cref="NexDragVisual.MoveSelf"/> element snaps back when the drop is not
        /// accepted by anything.
        /// </summary>
        public bool ReturnOnFailedDrop = true;

        private int _wanted;

        // ---- drag visual state ----------------------------------------------

        private GameObject _ghost;
        private RectTransform _dragged;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Vector3 _originalPosition;
        private CanvasGroup _selfGroup;
        private bool _selfGroupWasAdded;
        private bool _selfGroupBlocked;
        private Canvas _canvas;

        public void Want(NexTrigger trigger) => _wanted |= 1 << (int)trigger;

        private bool Wants(NexTrigger trigger) => (_wanted & (1 << (int)trigger)) != 0;

        private void Raise(NexTrigger trigger)
        {
            if (Wants(trigger)) Triggered?.Invoke(trigger);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // The press this drag grew out of is no longer a press.
            GetComponent<NXInteractionRelay>()?.CancelPress();

            BeginVisual(eventData);
            DragSourceChanged?.Invoke(true);
            Raise(NexTrigger.OnDragBegin);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MoveVisual(eventData);
            Raise(NexTrigger.OnDrag);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EndVisual(eventData);
            Raise(NexTrigger.OnDragEnd);

            // Cleared after the trigger, and after any drop: uGUI delivers OnDrop before OnEndDrag,
            // so the source stays readable while the receiving rule runs and is gone before the
            // next gesture begins.
            DragSourceChanged?.Invoke(false);
        }

        private void OnDisable()
        {
            // Being hidden mid-drag would otherwise leave a ghost on the canvas forever and the
            // source published, so the next unrelated drop rule would read it as if live.
            EndVisual(null);
            DragSourceChanged?.Invoke(false);
        }

        // ---- drag visual -----------------------------------------------------

        private void BeginVisual(PointerEventData eventData)
        {
            if (Visual == NexDragVisual.None) return;

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) return;

            // The root canvas, not the nearest one: a nested canvas is often the very panel being
            // dragged out of, and parenting to it would keep the dragged thing clipped inside.
            var root = _canvas.rootCanvas != null ? _canvas.rootCanvas : _canvas;

            if (Visual == NexDragVisual.Ghost)
            {
                _ghost = Instantiate(gameObject, root.transform);

                // The copy must not act like the original: its relays would raise triggers for a
                // node index it does not own, and its layout component would fight the pointer.
                foreach (var relay in _ghost.GetComponentsInChildren<NXInteractionDragRelay>(true)) Destroy(relay);
                foreach (var relay in _ghost.GetComponentsInChildren<NXInteractionRelay>(true)) Destroy(relay);
                foreach (var layout in _ghost.GetComponentsInChildren<UnityEngine.UI.LayoutElement>(true))
                    Destroy(layout);

                var ghostGroup = _ghost.GetComponent<CanvasGroup>();
                if (ghostGroup == null) ghostGroup = _ghost.AddComponent<CanvasGroup>();
                ghostGroup.alpha = Mathf.Clamp01(GhostOpacity);

                // The single most important line here. A visual sitting under the cursor is what
                // the raycaster hits, so without this the drop target is never found and OnDrop
                // never fires - the classic drag-and-drop bug.
                ghostGroup.blocksRaycasts = false;

                _dragged = _ghost.transform as RectTransform;
            }
            else
            {
                _dragged = transform as RectTransform;
                if (_dragged == null) return;

                _originalParent = _dragged.parent;
                _originalSiblingIndex = _dragged.GetSiblingIndex();
                _originalPosition = _dragged.position;

                // Reparented so the dragged element draws above everything and escapes any mask
                // or clipping rect it was living inside.
                _dragged.SetParent(root.transform, true);
                _dragged.SetAsLastSibling();

                _selfGroup = GetComponent<CanvasGroup>();
                if (_selfGroup == null)
                {
                    _selfGroup = gameObject.AddComponent<CanvasGroup>();
                    _selfGroupWasAdded = true;
                }

                // Remembered rather than assumed: an element may already have had raycasts off,
                // and restoring it to "on" would silently change how it behaves after the drag.
                _selfGroupBlocked = _selfGroup.blocksRaycasts;
                _selfGroup.blocksRaycasts = false;
            }

            MoveVisual(eventData);
        }

        private void MoveVisual(PointerEventData eventData)
        {
            if (_dragged == null || eventData == null || _canvas == null) return;

            var root = _canvas.rootCanvas != null ? _canvas.rootCanvas : _canvas;

            // Overlay canvases take screen coordinates directly; every other mode needs the
            // pointer projected onto the canvas plane through the camera that renders it.
            if (root.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _dragged.position = eventData.position;
                return;
            }

            var camera = root.worldCamera != null ? root.worldCamera : eventData.pressEventCamera;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    root.transform as RectTransform, eventData.position, camera, out var world))
            {
                _dragged.position = world;
            }
        }

        private void EndVisual(PointerEventData eventData)
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
            else if (Visual == NexDragVisual.MoveSelf && _dragged != null)
            {
                if (ReturnOnFailedDrop && !WasAccepted(eventData)) Restore();
                else if (_originalParent != null && _dragged.parent == null) Restore();
            }

            if (_selfGroup != null)
            {
                if (_selfGroupWasAdded) Destroy(_selfGroup);
                else _selfGroup.blocksRaycasts = _selfGroupBlocked;

                _selfGroup = null;
                _selfGroupWasAdded = false;
            }

            _dragged = null;
            _originalParent = null;
            _canvas = null;
        }

        private void Restore()
        {
            if (_originalParent == null) return;

            _dragged.SetParent(_originalParent, true);
            _dragged.SetSiblingIndex(_originalSiblingIndex);
            _dragged.position = _originalPosition;
        }

        /// <summary>
        /// Whether the drop landed on something that can receive it.
        /// </summary>
        /// <remarks>
        /// Read from the raycast rather than from whether a rule ran: a drop target may well
        /// refuse the item in its own condition, and that is still a drop that landed somewhere.
        /// Snapping back would then fight a rule that had already decided what to do.
        /// </remarks>
        private bool WasAccepted(PointerEventData eventData)
        {
            var hit = eventData?.pointerCurrentRaycast.gameObject;
            return hit != null && hit.GetComponentInParent<NXInteractionRelay>() != null;
        }
    }
}
