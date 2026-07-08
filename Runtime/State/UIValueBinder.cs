using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>Binds a float state value to an element's <see cref="IUIValueCapability"/>.</summary>
    public sealed class UIValueBinder : UIBinder
    {
        private IDisposable _watch;

        public override void Bind(IUIElementHandle target, string key, UIStateStore store)
        {
            var cap = Require<IUIValueCapability>(target, nameof(UIValueBinder));
            if (cap == null || store == null) return;

            _watch = store.Watch<float>(key, v => cap.Value = v);
        }

        public override void Unbind()
        {
            _watch?.Dispose();
            _watch = null;
        }
    }
}
