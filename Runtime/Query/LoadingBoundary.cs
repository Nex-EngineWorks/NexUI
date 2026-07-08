using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.State;

namespace emiteat.NexUI.Query
{
    /// <summary>
    /// Shows a target element only while a query is loading, via
    /// <see cref="IUIVisibilityCapability"/>. Backend-independent.
    /// </summary>
    public sealed class LoadingBoundary<T> : IDisposable
    {
        private readonly IDisposable _sub;

        public LoadingBoundary(UISignal<QueryState<T>> state, IUIElementHandle loadingElement)
        {
            var cap = loadingElement?.As<IUIVisibilityCapability>();
            if (state == null || cap == null)
            {
                if (cap == null && loadingElement != null)
                    UnityEngine.Debug.LogWarning(
                        $"[NexUI] LoadingBoundary: '{loadingElement.Id}' has no IUIVisibilityCapability.");
                _sub = null;
                return;
            }

            _sub = state.Subscribe(s => cap.Visible = s.IsLoading);
        }

        public void Dispose() => _sub?.Dispose();
    }
}
