using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// One-way binds an <see cref="IBindableProperty{TSource}"/> data-source property to an
    /// element's <see cref="IUITextCapability"/>, through an
    /// <see cref="IValueConverter{TSource, TTarget}"/> when supplied, or <see cref="object.ToString"/>
    /// otherwise.
    /// </summary>
    public sealed class PropertyTextBinder<TSource> : PropertyBinder<TSource>
    {
        private readonly IValueConverter<TSource, string> _converter;
        private IBindableProperty<TSource> _source;
        private Action<TSource> _handler;
        private IUITextInputCapability _input;
        private Action<string> _inputHandler;
        private readonly UIBindingMode _mode;
        private bool _syncing;

        public PropertyTextBinder(IValueConverter<TSource, string> converter = null,
            UIBindingMode mode = UIBindingMode.OneWay)
        {
            _converter = converter;
            _mode = mode;
        }

        public override void Bind(IUIElementHandle target, IBindableProperty<TSource> source)
        {
            var cap = Require<IUITextCapability>(target, nameof(PropertyTextBinder<TSource>));
            if (cap == null || source == null) return;

            Unbind();
            _source = source;
            if (_mode != UIBindingMode.OneWayToSource)
            {
                _handler = v =>
                {
                    if (_syncing) return;
                    _syncing = true;
                    cap.Text = ConvertToText(v);
                    _syncing = false;
                };
                _source.ValueChanged += _handler;
                cap.Text = ConvertToText(_source.Value);
            }

            if (_mode != UIBindingMode.OneWay)
            {
                _input = target.As<IUITextInputCapability>();
                if (_input == null) return;
                _inputHandler = value =>
                {
                    if (_syncing) return;
                    _syncing = true;
                    _source.Value = ConvertFromText(value);
                    _syncing = false;
                };
                _input.TextChanged += _inputHandler;
                if (_mode == UIBindingMode.OneWayToSource) _inputHandler(_input.Text);
            }
        }

        public override void Unbind()
        {
            if (_source != null && _handler != null)
                _source.ValueChanged -= _handler;
            _source = null;
            _handler = null;
            if (_input != null && _inputHandler != null) _input.TextChanged -= _inputHandler;
            _input = null;
            _inputHandler = null;
        }

        private string ConvertToText(TSource value)
            => _converter != null ? _converter.Convert(value) : value?.ToString() ?? string.Empty;

        private TSource ConvertFromText(string value)
        {
            if (_converter != null) return _converter.ConvertBack(value);
            if (typeof(TSource) == typeof(string)) return (TSource)(object)value;
            throw new InvalidOperationException($"[NexUI] Two-way text binding for {typeof(TSource).Name} requires a converter.");
        }
    }
}
