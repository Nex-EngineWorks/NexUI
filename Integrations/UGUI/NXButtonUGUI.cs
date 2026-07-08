using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>uGUI implementation of <see cref="INXButton"/> over an element handle.</summary>
    public sealed class NXButtonUGUI : INXButton
    {
        private readonly IUIClickCapability _click;
        private readonly IUIInteractableCapability _interactable;

        public IUIElementHandle Handle { get; }

        public event Action Clicked
        {
            add { if (_click != null) _click.Clicked += value; }
            remove { if (_click != null) _click.Clicked -= value; }
        }

        public bool Interactable
        {
            get => _interactable?.Interactable ?? false;
            set { if (_interactable != null) _interactable.Interactable = value; }
        }

        public NXButtonUGUI(IUIElementHandle handle)
        {
            Handle = handle;
            _click = handle?.As<IUIClickCapability>();
            _interactable = handle?.As<IUIInteractableCapability>();

            if (_click == null)
                UnityEngine.Debug.LogWarning($"[NexUI] NXButtonUGUI: '{handle?.Id}' has no click capability.");
        }
    }
}
