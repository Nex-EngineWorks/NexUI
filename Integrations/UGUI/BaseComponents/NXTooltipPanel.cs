using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
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
}
