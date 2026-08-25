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
    /// A panel that opens next to something - overflow menus, item detail cards, filter dropdowns.
    /// </summary>
    /// <remarks>
    /// Placement flips to the other side of the anchor when the preferred side would leave the
    /// canvas, which is the behaviour that separates a popover from "a panel someone positioned by
    /// hand and that breaks on a different aspect ratio".
    /// </remarks>
    [AddComponentMenu("NexUI/Overlay/NX Popover")]
    public sealed class NXPopover : UIBehaviour, INXPopover
    {
        public enum Side { Below, Above, Left, Right }

        [SerializeField] private Side m_PreferredSide = Side.Below;
        [SerializeField, Tooltip("Gap between the anchor and this panel, in pixels.")]
        private float m_Offset = 8f;
        [SerializeField, Tooltip("Canvas the placement is clamped inside. Defaults to the parent canvas.")]
        private RectTransform m_Bounds;

        [SerializeField] private UnityEvent m_OnClosed = new UnityEvent();

        private RectTransform _rect;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public bool IsOpen { get; private set; }

        /// <inheritdoc/>
        public event Action Closed;

        /// <summary>Inspector-friendly mirror of <see cref="Closed"/>.</summary>
        public UnityEvent OnClosed => m_OnClosed;

        protected override void Awake()
        {
            base.Awake();
            _rect = transform as RectTransform;
            if (m_Bounds != null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) m_Bounds = canvas.transform as RectTransform;
        }

        /// <inheritdoc/>
        public void Open(IUIElementHandle anchor)
        {
            gameObject.SetActive(true);
            IsOpen = true;

            var anchorRect = UGUIHandleRect.Resolve(anchor);
            if (anchorRect != null) PlaceNear(anchorRect);
        }

        /// <inheritdoc/>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            gameObject.SetActive(false);
            Closed?.Invoke();
            m_OnClosed.Invoke();
        }

        private void PlaceNear(RectTransform anchor)
        {
            if (_rect == null) return;

            var side = m_PreferredSide;
            var placed = Place(anchor, side);

            // One flip is enough: the opposite side of an anchor that is itself on screen always
            // has room unless the popover is larger than the canvas, and in that case no side works.
            if (m_Bounds != null && !FitsInside(placed))
                placed = Place(anchor, Opposite(side));

            _rect.position = placed;
        }

        private Vector3 Place(RectTransform anchor, Side side)
        {
            var anchorCorners = new Vector3[4];
            anchor.GetWorldCorners(anchorCorners);
            var anchorCentre = (anchorCorners[0] + anchorCorners[2]) * 0.5f;
            var anchorSize = anchorCorners[2] - anchorCorners[0];

            var selfCorners = new Vector3[4];
            _rect.GetWorldCorners(selfCorners);
            var selfSize = selfCorners[2] - selfCorners[0];

            switch (side)
            {
                case Side.Above:
                    return anchorCentre + new Vector3(0f, (anchorSize.y + selfSize.y) * 0.5f + m_Offset, 0f);
                case Side.Left:
                    return anchorCentre - new Vector3((anchorSize.x + selfSize.x) * 0.5f + m_Offset, 0f, 0f);
                case Side.Right:
                    return anchorCentre + new Vector3((anchorSize.x + selfSize.x) * 0.5f + m_Offset, 0f, 0f);
                default:
                    return anchorCentre - new Vector3(0f, (anchorSize.y + selfSize.y) * 0.5f + m_Offset, 0f);
            }
        }

        private static Side Opposite(Side side) => side switch
        {
            Side.Above => Side.Below,
            Side.Below => Side.Above,
            Side.Left => Side.Right,
            _ => Side.Left
        };

        private bool FitsInside(Vector3 candidate)
        {
            var boundsCorners = new Vector3[4];
            m_Bounds.GetWorldCorners(boundsCorners);

            var selfCorners = new Vector3[4];
            _rect.GetWorldCorners(selfCorners);
            var half = (selfCorners[2] - selfCorners[0]) * 0.5f;

            return candidate.x - half.x >= boundsCorners[0].x
                && candidate.x + half.x <= boundsCorners[2].x
                && candidate.y - half.y >= boundsCorners[0].y
                && candidate.y + half.y <= boundsCorners[2].y;
        }
    }
}
