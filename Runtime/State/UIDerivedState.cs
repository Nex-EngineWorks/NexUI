using System;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// A read-only value computed from a source signal via a selector. Recomputes
    /// whenever the source changes and exposes the result as its own signal.
    /// </summary>
    public sealed class UIDerivedState<TSource, TResult> : IDisposable
    {
        private readonly UISignal<TResult> _result;
        private readonly IDisposable _subscription;

        public UIDerivedState(UISignal<TSource> source, Func<TSource, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            _result = new UISignal<TResult>(selector(source.Value));
            _subscription = source.Subscribe(v => _result.Value = selector(v), fireImmediately: false);
        }

        public TResult Value => _result.Value;

        public IDisposable Subscribe(Action<TResult> listener, bool fireImmediately = true)
            => _result.Subscribe(listener, fireImmediately);

        public void Dispose() => _subscription?.Dispose();
    }
}
