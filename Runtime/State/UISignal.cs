using System;
using System.Collections.Generic;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// A standalone observable value. Complementary to <see cref="UIStateStore"/> for
    /// cases where a strongly-typed reactive field is preferable to a keyed store.
    /// </summary>
    public sealed class UISignal<T>
    {
        private T _value;
        private readonly List<Action<T>> _listeners = new List<Action<T>>();
        private readonly IEqualityComparer<T> _comparer;

        public UISignal(T initial = default, IEqualityComparer<T> comparer = null)
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
                for (int i = _listeners.Count - 1; i >= 0; i--)
                    _listeners[i]?.Invoke(value);
            }
        }

        /// <summary>Subscribe; fires immediately with the current value. Dispose to unsubscribe.</summary>
        public IDisposable Subscribe(Action<T> listener, bool fireImmediately = true)
        {
            if (listener == null) return EmptyDisposable.Instance;
            _listeners.Add(listener);
            if (fireImmediately) listener(_value);
            return new Unsub(() => _listeners.Remove(listener));
        }

        private sealed class Unsub : IDisposable
        {
            private Action _a;
            public Unsub(Action a) => _a = a;
            public void Dispose() { _a?.Invoke(); _a = null; }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }
    }
}
