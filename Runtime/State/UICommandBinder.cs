using System;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// Binds an element's <see cref="IUIClickCapability"/> to an action key resolved
    /// through a <see cref="UIActionResolver"/>. The state store is unused here but the
    /// signature matches <see cref="UIBinder"/>; pass the action key as <c>key</c>.
    /// </summary>
    public sealed class UICommandBinder : UIBinder
    {
        private readonly UIActionResolver _resolver;
        private IUIClickCapability _cap;
        private Action _handler;
        private string _actionKey;

        public UICommandBinder(UIActionResolver resolver) => _resolver = resolver;

        public override void Bind(IUIElementHandle target, string key, UIStateStore store)
        {
            _cap = Require<IUIClickCapability>(target, nameof(UICommandBinder));
            if (_cap == null) return;

            if (_resolver == null)
            {
                Debug.LogWarning($"[NexUI] UICommandBinder for '{key}' has no UIActionResolver.");
                return;
            }

            _actionKey = key;
            _handler = () => _ = _resolver.ExecuteAsync(_actionKey);
            _cap.Clicked += _handler;
        }

        public override void Unbind()
        {
            if (_cap != null && _handler != null)
                _cap.Clicked -= _handler;
            _cap = null;
            _handler = null;
        }
    }
}
