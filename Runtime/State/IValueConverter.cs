namespace emiteat.NexUI.State
{
    /// <summary>
    /// Converts between a data-source value type and a UI-facing value type for
    /// <see cref="PropertyBinder{TSource, TTarget}"/>. Implement <see cref="ConvertBack"/> only
    /// when the binding is two-way; one-way-only converters may throw
    /// <see cref="System.NotSupportedException"/> from it.
    /// </summary>
    public interface IValueConverter<TSource, TTarget>
    {
        TTarget Convert(TSource source);
        TSource ConvertBack(TTarget target);
    }
}
