using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>Binds a bool state value to an element's <see cref="IUIVisibilityCapability"/>.</summary>
    public sealed class UIVisibilityBinder : UIBinder
    {
        private IDisposable _watch;
        private readonly bool _invert;

        public UIVisibilityBinder(bool invert = false) => _invert = invert;

        public override void Bind(IUIElementHandle target, string key, UIStateStore store)
        {
            var cap = Require<IUIVisibilityCapability>(target, nameof(UIVisibilityBinder));
            if (cap == null || store == null) return;

            _watch = store.Watch<bool>(key, v => cap.Visible = _invert ? !v : v);
        }

        public override void Unbind()
        {
            _watch?.Dispose();
            _watch = null;
        }
    }
}
