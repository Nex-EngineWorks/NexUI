using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// Toggles a style class on an element based on a bool state value, using
    /// <see cref="IUIStyleCapability"/>.
    /// </summary>
    public sealed class UIClassBinder : UIBinder
    {
        private IDisposable _watch;
        private readonly string _className;
        private readonly bool _invert;

        public UIClassBinder(string className, bool invert = false)
        {
            _className = className;
            _invert = invert;
        }

        public override void Bind(IUIElementHandle target, string key, UIStateStore store)
        {
            var cap = Require<IUIStyleCapability>(target, nameof(UIClassBinder));
            if (cap == null || store == null) return;

            _watch = store.Watch<bool>(key, v => cap.SetClass(_className, _invert ? !v : v));
        }

        public override void Unbind()
        {
            _watch?.Dispose();
            _watch = null;
        }
    }
}
