using System;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// A single observable value slot on a data source. Setting <see cref="Value"/> raises
    /// <see cref="ValueChanged"/> so bound UI elements update automatically - the
    /// backend-agnostic building block <see cref="PropertyBinder{T}"/> binds to. Complements
    /// <see cref="UIStateStore"/> (a central string-keyed store): an
    /// <see cref="IBindableProperty{T}"/> is instead a direct reference to a single field on a
    /// plain data-source object, with no string-key lookup involved.
    /// </summary>
    public interface IBindableProperty<T>
    {
        T Value { get; set; }
        event Action<T> ValueChanged;
    }
}
