using Cysharp.Threading.Tasks;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>Dispatches commands through the registered middleware chain to handlers.</summary>
    public interface IUICommandDispatcher
    {
        UniTask DispatchAsync(IUICommand command);
        void UseMiddleware(IUIMiddleware middleware);
        void RegisterHandler<TCommand>(IUICommandHandler<TCommand> handler) where TCommand : IUICommand;
    }
}
