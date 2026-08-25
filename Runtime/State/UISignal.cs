using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// A standalone observable value. Complementary to <see cref="UIStateStore"/> for
    /// cases where a strongly-typed reactive field is preferable to a keyed store.
    /// </summary>
    public sealed class UISignal<T>
    {
        private sealed class Listener
        {
            public Action<T> Handler;
            public bool RemovedPending;
        }

        private T _value;
        private readonly List<Listener> _listeners = new List<Listener>();
        private readonly IEqualityComparer<T> _comparer;
        /// <summary>>0 while a dispatch is running; removals defer to the end of the outermost one.</summary>
        private int _dispatchDepth;

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

                // Allocation-free dispatch. The watermark excludes subscribers added during the
                // dispatch; removals are deferred so a listener unsubscribing mid-dispatch still
                // receives this value - identical to the old ToArray() snapshot semantics without
                // the array.
                _dispatchDepth++;
                var watermark = _listeners.Count;
                try
                {
                    for (int i = 0; i < watermark; i++)
                    {
                        var handler = _listeners[i].Handler;
                        try { handler?.Invoke(value); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                }
                finally
                {
                    _dispatchDepth--;
                    if (_dispatchDepth == 0)
                        _listeners.RemoveAll(IsRemovedPending);
                }
            }
        }

        /// <summary>Subscribe; fires immediately with the current value. Dispose to unsubscribe.</summary>
        public IDisposable Subscribe(Action<T> listener, bool fireImmediately = true)
        {
            if (listener == null) return EmptyDisposable.Instance;
            var node = new Listener { Handler = listener };
            _listeners.Add(node);
            if (fireImmediately)
            {
                try { listener(_value); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            return new Unsub(() =>
            {
                if (_dispatchDepth > 0) node.RemovedPending = true;
                else _listeners.Remove(node);
            });
        }

        private static bool IsRemovedPending(Listener node) => node.RemovedPending;

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
