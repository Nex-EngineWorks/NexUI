using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>Binds a state value to an element's <see cref="IUITextCapability"/>.</summary>
    public sealed class UITextBinder : UIBinder
    {
        private IDisposable _watch;
        private readonly Func<object, string> _format;
        private readonly Func<string, object> _parse;
        private readonly UIBindingMode _mode;
        private IUITextInputCapability _input;
        private Action<string> _inputHandler;
        private UIStateStore _store;
        private string _key;
        private bool _syncing;
        private readonly UIBindingConverterRegistry _converters;
        private readonly string _converterKey;
        private IUIBindingConverter _converter;

        public UITextBinder(Func<object, string> format = null)
            : this(UIBindingMode.OneWay, format) { }

        public UITextBinder(UIBindingMode mode, Func<object, string> format = null,
            Func<string, object> parse = null)
        {
            _mode = mode;
            _format = format ?? (o => o?.ToString() ?? string.Empty);
            _parse = parse ?? (text => text);
        }

        public UITextBinder(UIBindingMode mode, string converterKey, UIBindingConverterRegistry converters)
            : this(mode)
        {
            _converterKey = converterKey;
            _converters = converters;
        }

        public override void Bind(IUIElementHandle target, string key, UIStateStore store)
        {
            var cap = Require<IUITextCapability>(target, nameof(UITextBinder));
            if (cap == null || store == null) return;

            Unbind();
            _store = store;
            _key = key;
            if (!string.IsNullOrEmpty(_converterKey) &&
                (_converters == null || !_converters.TryResolve(_converterKey, out _converter)))
                UnityEngine.Debug.LogWarning($"[NexUI] UITextBinder converter '{_converterKey}' is not registered.");

            if (_mode != UIBindingMode.OneWayToSource)
                _watch = store.Watch<object>(key, v =>
                {
                    if (_syncing) return;
                    _syncing = true;
                    cap.Text = _converter != null
                        ? _converter.Convert(v)?.ToString() ?? string.Empty
                        : _format(v);
                    _syncing = false;
                });

            if (_mode != UIBindingMode.OneWay)
            {
                _input = target.As<IUITextInputCapability>();
                if (_input == null)
                {
                    UnityEngine.Debug.LogWarning($"[NexUI] UITextBinder: element '{target.Id}' is not editable; {_mode} is inactive.");
                    return;
                }
                _inputHandler = text =>
                {
                    if (_syncing) return;
                    _syncing = true;
                    _store.Set(_key, _converter != null ? _converter.ConvertBack(text) : _parse(text));
                    _syncing = false;
                };
                _input.TextChanged += _inputHandler;
                if (_mode == UIBindingMode.OneWayToSource) _inputHandler(_input.Text);
            }
        }

        public override void Unbind()
        {
            _watch?.Dispose();
            _watch = null;
            if (_input != null && _inputHandler != null) _input.TextChanged -= _inputHandler;
            _input = null;
            _inputHandler = null;
            _store = null;
            _key = null;
            _converter = null;
        }
    }
}
