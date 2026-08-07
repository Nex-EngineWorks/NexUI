using System;
using System.Threading.Tasks;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Pipeline middleware wrapping command execution. Call <paramref name="next"/>
    /// to continue the chain, or short-circuit by not calling it.
    /// </summary>
    public interface IUIMiddleware
    {
        Task InvokeAsync(
            IUICommand command,
            UICommandContext context,
            Func<Task> next
        );
    }
}
