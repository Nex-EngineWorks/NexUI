using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.State;

namespace emiteat.NexUI.Query
{
    /// <summary>
    /// Shows a target element when a query errors and optionally routes the error message
    /// to an <see cref="IUITextCapability"/>. Backend-independent.
    /// </summary>
    public sealed class ErrorBoundary<T> : IDisposable
    {
        private readonly IDisposable _sub;

        /// <summary>Raised with the error message whenever the query enters the error state.</summary>
        public event Action<string> ErrorRaised;

        public ErrorBoundary(UISignal<QueryState<T>> state, IUIElementHandle errorElement)
        {
            var visibility = errorElement?.As<IUIVisibilityCapability>();
            var text = errorElement?.As<IUITextCapability>();

            if (state == null || visibility == null)
            {
                _sub = null;
                return;
            }

            _sub = state.Subscribe(s =>
            {
                visibility.Visible = s.IsError;
                if (s.IsError)
                {
                    if (text != null) text.Text = s.Error;
                    ErrorRaised?.Invoke(s.Error);
                }
            });
        }

        public void Dispose() => _sub?.Dispose();
    }
}
