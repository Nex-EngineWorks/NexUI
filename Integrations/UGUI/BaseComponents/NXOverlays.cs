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

    /// <summary>
    /// A modal surface: it reports that the player asked to leave, and lets whoever owns the screen
    /// stack decide what that means.
    /// </summary>
    /// <remarks>
    /// It deliberately does not close itself. A modal that hides on backdrop click looks correct
    /// until the first "you have unsaved changes" confirmation, at which point the panel is already
    /// gone and the prompt has nothing to return to. Raising <see cref="CloseRequested"/> with a
    /// reason keeps that decision with the caller, and <see cref="Close"/> stays available for the
    /// common case where there is nothing to ask.
    /// </remarks>
    [AddComponentMenu("NexUI/Overlay/NX Modal")]
    public sealed class NXModal : UIBehaviour, INXModal
    {
        /// <summary>Reason string raised when the backdrop was clicked.</summary>
        public const string BackdropReason = "backdrop";

        [SerializeField, Tooltip("Element that dims the content behind. Clicking it requests a close.")]
        private Graphic m_Backdrop;
        [SerializeField, Tooltip("Panel shown and hidden. Defaults to this element.")]
        private GameObject m_Panel;
        [SerializeField] private bool m_CloseOnBackdropClick = true;
        [SerializeField] private bool m_OpenOnEnable = true;

        [SerializeField] private UnityEvent<string> m_OnCloseRequested = new UnityEvent<string>();

        private NXBackdropRelay _relay;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public bool IsOpen { get; private set; }

        /// <inheritdoc/>
        public event Action<string> CloseRequested;

        /// <summary>Inspector-friendly mirror of <see cref="CloseRequested"/>.</summary>
        public UnityEvent<string> OnCloseRequested => m_OnCloseRequested;

        protected override void Awake()
        {
            base.Awake();
            if (m_Panel == null) m_Panel = gameObject;
            if (m_Backdrop == null) return;

            _relay = m_Backdrop.GetComponent<NXBackdropRelay>();
            if (_relay == null) _relay = m_Backdrop.gameObject.AddComponent<NXBackdropRelay>();
            _relay.Clicked = () => { if (m_CloseOnBackdropClick) RequestClose(BackdropReason); };
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_OpenOnEnable) Open();
        }

        public void Open()
        {
            IsOpen = true;
            if (m_Backdrop != null) m_Backdrop.gameObject.SetActive(true);
            if (m_Panel != null && m_Panel != gameObject) m_Panel.SetActive(true);
        }

        /// <summary>Hides the modal without asking anyone. Use after a <see cref="CloseRequested"/> was accepted.</summary>
        public void Close()
        {
            IsOpen = false;
            if (m_Backdrop != null) m_Backdrop.gameObject.SetActive(false);
            if (m_Panel != null && m_Panel != gameObject) m_Panel.SetActive(false);
            else if (m_Panel == gameObject) gameObject.SetActive(false);
        }

        /// <inheritdoc/>
        public void RequestClose(string reason = null)
        {
            if (!IsOpen) return;
            var value = reason ?? string.Empty;
            CloseRequested?.Invoke(value);
            m_OnCloseRequested.Invoke(value);
        }
    }

    /// <summary>Forwards a click on a backdrop graphic without making the backdrop a Button.</summary>
    /// <remarks>
    /// A Button would come with navigation, transitions and a selectable state, all of which are
    /// wrong for a dimmer: it would take keyboard focus away from the modal's own controls.
    /// </remarks>
    [AddComponentMenu("")]
    internal sealed class NXBackdropRelay : UIBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }

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

    /// <summary>
    /// The tooltip panel itself - the thing <see cref="NXTooltipTrigger"/> shows.
    /// </summary>
    /// <remarks>
    /// Split from the trigger because a screen wants one tooltip panel and many triggers. Merging
    /// them, which is the obvious first design, gives every hoverable element its own hidden panel
    /// and its own text layout.
    /// </remarks>
    [AddComponentMenu("NexUI/Overlay/NX Tooltip Panel")]
    public sealed class NXTooltipPanel : UIBehaviour, INXTooltip
    {
        [SerializeField, TextArea] private string m_Text = "";
        [SerializeField, Tooltip("Label the text is written into.")] private Graphic m_Label;
        [SerializeField, Tooltip("Gap between the anchor and the tooltip, in pixels.")]
        private float m_Offset = 6f;

        private RectTransform _rect;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public bool IsVisible { get; private set; }

        /// <inheritdoc/>
        public string Text
        {
            get => m_Text;
            set { m_Text = value; ApplyText(); }
        }

        protected override void Awake()
        {
            base.Awake();
            _rect = transform as RectTransform;
            ApplyText();
            gameObject.SetActive(false);
        }

        /// <inheritdoc/>
        public void Show(IUIElementHandle anchor)
        {
            gameObject.SetActive(true);
            IsVisible = true;
            ApplyText();

            var anchorRect = UGUIHandleRect.Resolve(anchor);
            if (anchorRect == null || _rect == null) return;

            var anchorCorners = new Vector3[4];
            anchorRect.GetWorldCorners(anchorCorners);
            var selfCorners = new Vector3[4];
            _rect.GetWorldCorners(selfCorners);

            var anchorCentre = (anchorCorners[0] + anchorCorners[2]) * 0.5f;
            var lift = (anchorCorners[2].y - anchorCorners[0].y + selfCorners[2].y - selfCorners[0].y) * 0.5f;
            _rect.position = anchorCentre + new Vector3(0f, lift + m_Offset, 0f);
        }

        /// <inheritdoc/>
        public void Hide()
        {
            IsVisible = false;
            gameObject.SetActive(false);
        }

        private void ApplyText()
        {
            if (m_Label == null) return;
            var text = m_Label.GetComponent<TMPro.TMP_Text>();
            if (text != null) { text.text = m_Text; return; }
            if (m_Label is Text legacy) legacy.text = m_Text;
        }
    }

    /// <summary>
    /// A named mount point inside a reusable component - the "put your content here" hole a card,
    /// a dialog or a list row leaves for its caller.
    /// </summary>
    /// <remarks>
    /// Reparenting keeps the incoming content's own layout properties rather than copying them,
    /// so a slot never silently resizes what it was handed. <see cref="Clear"/> only detaches what
    /// this slot placed: destroying arbitrary children would take out the placeholder art that
    /// makes an empty slot visible while authoring.
    /// </remarks>
    [AddComponentMenu("NexUI/Layout/NX Slot")]
    public sealed class NXSlot : UIBehaviour, INXSlot
    {
        [SerializeField, Tooltip("Name the owning component routes content to.")]
        private string m_SlotName = "content";
        [SerializeField, Tooltip("Shown while the slot is empty.")] private GameObject m_Placeholder;

        private RectTransform _content;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public string SlotName => m_SlotName;

        /// <inheritdoc/>
        public bool HasContent => _content != null;

        /// <inheritdoc/>
        public void SetContent(IUIElementHandle content)
        {
            Clear();

            _content = UGUIHandleRect.Resolve(content);
            if (_content == null) return;

            _content.SetParent(transform, worldPositionStays: false);
            if (m_Placeholder != null) m_Placeholder.SetActive(false);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            if (_content != null)
            {
                _content.SetParent(null, worldPositionStays: false);
                _content = null;
            }

            if (m_Placeholder != null) m_Placeholder.SetActive(true);
        }
    }
}
