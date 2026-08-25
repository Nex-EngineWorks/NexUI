using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Per-item event relay added to every realized view by <see cref="NXCollectionView"/>.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class NXCollectionItemRelay : UIBehaviour, IPointerClickHandler
    {
        internal NXCollectionView Owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Owner == null) return;
            var view = (RectTransform)transform;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Owner.ReportContext(view);
                return;
            }

            // Modifiers come through a probe rather than UnityEngine.Input, which throws outright on
            // a project configured for the Input System package alone.
            Owner.ReportClick(view, NXInputModifierProbe.IsAdditive, NXInputModifierProbe.IsRange);
        }
    }
}
