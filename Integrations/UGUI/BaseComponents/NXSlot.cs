using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
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
