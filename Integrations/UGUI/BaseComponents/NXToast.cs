using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Transient message with a severity and an auto-dismiss - "Saved", "Connection lost",
    /// "Item added". Unity has nothing for this, so every project rebuilds it.
    /// </summary>
    /// <remarks>
    /// The countdown runs on unscaled time and pauses while the pointer is over the toast, so a
    /// message does not vanish out from under someone who is reading it. Dismissal raises
    /// <see cref="Dismissed"/> rather than destroying the object: pooling toasts is the normal case
    /// and a component that destroys itself cannot be pooled.
    /// </remarks>
    [AddComponentMenu("NexUI/Feedback/NX Toast")]
    public sealed class NXToast : UIBehaviour, INXToast, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, TextArea] private string m_Message = "";
        [SerializeField] private NXToastSeverity m_Severity = NXToastSeverity.Info;
        [SerializeField, Tooltip("Seconds before it dismisses itself. 0 waits for an explicit Dismiss().")]
        private float m_Duration = 3f;
        [SerializeField, Tooltip("Label the message is written into.")] private Graphic m_Label;
        [SerializeField, Tooltip("Graphic tinted by severity.")] private Graphic m_Accent;

        [SerializeField] private Color m_InfoColor = new Color(0.25f, 0.55f, 0.95f);
        [SerializeField] private Color m_SuccessColor = new Color(0.25f, 0.72f, 0.42f);
        [SerializeField] private Color m_WarningColor = new Color(0.92f, 0.68f, 0.22f);
        [SerializeField] private Color m_ErrorColor = new Color(0.86f, 0.31f, 0.31f);

        [SerializeField] private UnityEvent m_OnDismissed = new UnityEvent();

        private float _remaining;
        private bool _counting;
        private bool _hovered;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <summary>Raised when the toast finished, however it finished.</summary>
        public UnityEvent Dismissed => m_OnDismissed;

        /// <inheritdoc/>
        public string Message
        {
            get => m_Message;
            set { m_Message = value; ApplyMessage(); }
        }

        /// <inheritdoc/>
        public NXToastSeverity Severity
        {
            get => m_Severity;
            set { m_Severity = value; ApplySeverity(); }
        }

        /// <inheritdoc/>
        public float Duration
        {
            get => m_Duration;
            set => m_Duration = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyMessage();
            ApplySeverity();
            Restart();
        }

        /// <summary>Starts the countdown again - what a pooled toast needs on reuse.</summary>
        public void Restart()
        {
            _remaining = m_Duration;
            _counting = m_Duration > 0f;
        }

        /// <summary>Ends the toast now and raises <see cref="Dismissed"/>.</summary>
        public void Dismiss()
        {
            if (!isActiveAndEnabled && !_counting) return;
            _counting = false;
            m_OnDismissed.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        private void Update()
        {
            if (!_counting || _hovered) return;
            _remaining -= UnityTime.unscaledDeltaTime;
            if (_remaining > 0f) return;
            Dismiss();
        }

        private void ApplyMessage()
        {
            if (m_Label == null) return;
            var text = m_Label.GetComponent<TMPro.TMP_Text>();
            if (text != null) { text.text = m_Message; return; }
            var legacy = m_Label as Text;
            if (legacy != null) legacy.text = m_Message;
        }

        private void ApplySeverity()
        {
            if (m_Accent == null) return;
            m_Accent.color = m_Severity switch
            {
                NXToastSeverity.Success => m_SuccessColor,
                NXToastSeverity.Warning => m_WarningColor,
                NXToastSeverity.Error => m_ErrorColor,
                _ => m_InfoColor
            };
        }
    }
}
