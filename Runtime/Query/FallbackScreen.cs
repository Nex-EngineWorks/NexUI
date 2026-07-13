using System;
using emiteat.NexUI.State;

namespace emiteat.NexUI.Query
{
    /// <summary>
    /// Declarative fallback routing for a query's error state. The Query module cannot
    /// reference Core, so instead of opening a screen directly it raises
    /// <see cref="FallbackRequested"/> with a screen id; the host wires this to
    /// <c>NexUIApp.Open(screenId)</c>.
    /// </summary>
    public sealed class FallbackScreen<T> : IDisposable
    {
        public string ScreenId { get; }

        /// <summary>Raised with the screen id to open when the query errors.</summary>
        public event Action<string> FallbackRequested;

        private readonly IDisposable _sub;
        private bool _requested;

        public FallbackScreen(UISignal<QueryState<T>> state, string screenId)
        {
            ScreenId = screenId;
            if (state == null) { _sub = null; return; }

            _sub = state.Subscribe(s =>
            {
                if (s.IsError && !_requested)
                {
                    _requested = true;
                    FallbackRequested?.Invoke(ScreenId);
                }
                else if (!s.IsError)
                {
                    _requested = false;
                }
            });
        }

        public void Dispose() => _sub?.Dispose();
    }
}
