using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>Handles a single command type.</summary>
    public interface IUICommandHandler<TCommand>
        where TCommand : IUICommand
    {
        Task HandleAsync(TCommand command, UICommandContext context);
    }
}
