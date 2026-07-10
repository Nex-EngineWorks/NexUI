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

        public PropertyTextBinder(IValueConverter<TSource, string> converter = null) => _converter = converter;

        public override void Bind(IUIElementHandle target, IBindableProperty<TSource> source)
        {
            var cap = Require<IUITextCapability>(target, nameof(PropertyTextBinder<TSource>));
            if (cap == null || source == null) return;

            _source = source;
            _handler = v => cap.Text = ConvertToText(v);
            _source.ValueChanged += _handler;
            cap.Text = ConvertToText(_source.Value);
        }

        public override void Unbind()
        {
            if (_source != null && _handler != null)
                _source.ValueChanged -= _handler;
            _source = null;
            _handler = null;
        }

        private string ConvertToText(TSource value)
            => _converter != null ? _converter.Convert(value) : value?.ToString() ?? string.Empty;
    }
}
