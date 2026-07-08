using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>Binds a state value to an element's <see cref="IUITextCapability"/>.</summary>
    public sealed class UITextBinder : UIBinder
    {
        private IDisposable _watch;
        private readonly Func<object, string> _format;

        public UITextBinder(Func<object, string> format = null)
            => _format = format ?? (o => o?.ToString() ?? string.Empty);

        public override void Bind(IUIElementHandle target, string key, UIStateStore store)
        {
            var cap = Require<IUITextCapability>(target, nameof(UITextBinder));
            if (cap == null || store == null) return;

            _watch = store.Watch<object>(key, v => cap.Text = _format(v));
        }

        public override void Unbind()
        {
            _watch?.Dispose();
            _watch = null;
        }
    }
}
