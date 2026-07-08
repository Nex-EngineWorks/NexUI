using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.State;

namespace emiteat.NexUI.Query
{
    /// <summary>
    /// Shows a target element when a query succeeds but returns no data, via
    /// <see cref="IUIVisibilityCapability"/>. Backend-independent.
    /// </summary>
    public sealed class EmptyBoundary<T> : IDisposable
    {
        private readonly IDisposable _sub;

        public EmptyBoundary(UISignal<QueryState<T>> state, IUIElementHandle emptyElement)
        {
            var cap = emptyElement?.As<IUIVisibilityCapability>();
            if (state == null || cap == null)
            {
                _sub = null;
                return;
            }

            _sub = state.Subscribe(s => cap.Visible = s.IsEmpty);
        }

        public void Dispose() => _sub?.Dispose();
    }
}
