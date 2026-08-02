using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// One-way binds an <see cref="IBindableProperty{TSource}"/> data-source property to an
    /// element's <see cref="IUIValueCapability"/> (progress bars, sliders, radial fills),
    /// through an <see cref="IValueConverter{TSource, TTarget}"/> when <typeparamref name="TSource"/>
    /// isn't already <see cref="float"/>.
    /// </summary>
    public sealed class PropertyValueBinder<TSource> : PropertyBinder<TSource>
    {
        private readonly IValueConverter<TSource, float> _converter;
        private IBindableProperty<TSource> _source;
        private Action<TSource> _handler;
        private IUIValueInputCapability _input;
        private Action<float> _inputHandler;
        private readonly UIBindingMode _mode;
        private bool _syncing;

        public PropertyValueBinder(IValueConverter<TSource, float> converter = null,
            UIBindingMode mode = UIBindingMode.OneWay)
        {
            _converter = converter;
            _mode = mode;
        }

        public override void Bind(IUIElementHandle target, IBindableProperty<TSource> source)
        {
            var cap = Require<IUIValueCapability>(target, nameof(PropertyValueBinder<TSource>));
            if (cap == null || source == null) return;

            Unbind();
            _source = source;
            if (_mode != UIBindingMode.OneWayToSource)
            {
                _handler = v =>
                {
                    if (_syncing) return;
                    _syncing = true;
                    cap.Value = ConvertToFloat(v);
                    _syncing = false;
                };
                _source.ValueChanged += _handler;
                cap.Value = ConvertToFloat(_source.Value);
            }

            if (_mode != UIBindingMode.OneWay)
            {
                _input = target.As<IUIValueInputCapability>();
                if (_input == null) return;
                _inputHandler = value =>
                {
                    if (_syncing) return;
                    _syncing = true;
                    _source.Value = ConvertFromFloat(value);
                    _syncing = false;
                };
                _input.ValueChanged += _inputHandler;
                if (_mode == UIBindingMode.OneWayToSource) _inputHandler(_input.Value);
            }
        }

        public override void Unbind()
        {
            if (_source != null && _handler != null)
                _source.ValueChanged -= _handler;
            _source = null;
            _handler = null;
            if (_input != null && _inputHandler != null) _input.ValueChanged -= _inputHandler;
            _input = null;
            _inputHandler = null;
        }

        private float ConvertToFloat(TSource value)
        {
            if (_converter != null) return _converter.Convert(value);
            if (value is float f) return f;
            throw new InvalidOperationException(
                $"[NexUI] PropertyValueBinder<{typeof(TSource).Name}> needs an " +
                $"IValueConverter<{typeof(TSource).Name}, float> because the source type isn't already float.");
        }

        private TSource ConvertFromFloat(float value)
        {
            if (_converter != null) return _converter.ConvertBack(value);
            if (typeof(TSource) == typeof(float)) return (TSource)(object)value;
            throw new InvalidOperationException($"[NexUI] Two-way value binding for {typeof(TSource).Name} requires a converter.");
        }
    }
}
