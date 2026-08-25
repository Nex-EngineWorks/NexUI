using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
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
}
