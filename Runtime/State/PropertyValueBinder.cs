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

        public PropertyValueBinder(IValueConverter<TSource, float> converter = null) => _converter = converter;

        public override void Bind(IUIElementHandle target, IBindableProperty<TSource> source)
        {
            var cap = Require<IUIValueCapability>(target, nameof(PropertyValueBinder<TSource>));
            if (cap == null || source == null) return;

            _source = source;
            _handler = v => cap.Value = ConvertToFloat(v);
            _source.ValueChanged += _handler;
            cap.Value = ConvertToFloat(_source.Value);
        }

        public override void Unbind()
        {
            if (_source != null && _handler != null)
                _source.ValueChanged -= _handler;
            _source = null;
            _handler = null;
        }

        private float ConvertToFloat(TSource value)
        {
            if (_converter != null) return _converter.Convert(value);
            if (value is float f) return f;
            throw new InvalidOperationException(
                $"[NexUI] PropertyValueBinder<{typeof(TSource).Name}> needs an " +
                $"IValueConverter<{typeof(TSource).Name}, float> because the source type isn't already float.");
        }
    }
}
