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
}
