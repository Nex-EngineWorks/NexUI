using System;
using System.Collections.Generic;

namespace emiteat.NexUI.State
{
    /// <summary>Default <see cref="IBindableProperty{T}"/> implementation backed by a plain field.</summary>
    public sealed class BindableProperty<T> : IBindableProperty<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private T _value;

        public BindableProperty(T initial = default, IEqualityComparer<T> comparer = null)
        {
            _value = initial;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (_comparer.Equals(_value, value)) return;
                _value = value;
                ValueChanged?.Invoke(_value);
            }
        }

        public event Action<T> ValueChanged;

        /// <summary>Sets the backing field without an equality check or a <see cref="ValueChanged"/> raise (initialization/reset).</summary>
        public void SetSilently(T value) => _value = value;
    }
}
