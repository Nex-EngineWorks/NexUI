using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>Binds a float state value to an element's <see cref="IUIValueCapability"/>.</summary>
    public sealed class UIValueBinder : UIBinder
    {
        private IDisposable _watch;
        private readonly UIBindingMode _mode;
        private IUIValueInputCapability _input;
        private Action<float> _inputHandler;
        private UIStateStore _store;
        private string _key;
        private bool _syncing;
        private readonly UIBindingConverterRegistry _converters;
        private readonly string _converterKey;
        private IUIBindingConverter _converter;

        public UIValueBinder(UIBindingMode mode = UIBindingMode.OneWay) => _mode = mode;

        public UIValueBinder(UIBindingMode mode, string converterKey, UIBindingConverterRegistry converters)
        {
            _mode = mode;
            _converterKey = converterKey;
            _converters = converters;
        }

        public override void Bind(IUIElementHandle target, string key, UIStateStore store)
        {
            var cap = Require<IUIValueCapability>(target, nameof(UIValueBinder));
            if (cap == null || store == null) return;

            Unbind();
            _store = store;
            _key = key;
            if (!string.IsNullOrEmpty(_converterKey) &&
                (_converters == null || !_converters.TryResolve(_converterKey, out _converter)))
                UnityEngine.Debug.LogWarning($"[NexUI] UIValueBinder converter '{_converterKey}' is not registered.");

            if (_mode != UIBindingMode.OneWayToSource)
            {
                if (_converter != null)
                    _watch = store.Watch<object>(key, v => ApplyToTarget(cap,
                        System.Convert.ToSingle(_converter.Convert(v), System.Globalization.CultureInfo.InvariantCulture)));
                else
                    _watch = store.Watch<float>(key, v => ApplyToTarget(cap, v));
            }

            if (_mode != UIBindingMode.OneWay)
            {
                _input = target.As<IUIValueInputCapability>();
                if (_input == null)
                {
                    UnityEngine.Debug.LogWarning($"[NexUI] UIValueBinder: element '{target.Id}' is not editable; {_mode} is inactive.");
                    return;
                }
                _inputHandler = value =>
                {
                    if (_syncing) return;
                    _syncing = true;
                    _store.Set(_key, _converter != null ? _converter.ConvertBack(value) : value);
                    _syncing = false;
                };
                _input.ValueChanged += _inputHandler;
                if (_mode == UIBindingMode.OneWayToSource) _inputHandler(_input.Value);
            }
        }

        public override void Unbind()
        {
            _watch?.Dispose();
            _watch = null;
            if (_input != null && _inputHandler != null) _input.ValueChanged -= _inputHandler;
            _input = null;
            _inputHandler = null;
            _store = null;
            _key = null;
            _converter = null;
        }

        private void ApplyToTarget(IUIValueCapability cap, float value)
        {
            if (_syncing) return;
            _syncing = true;
            cap.Value = value;
            _syncing = false;
        }
    }
}
