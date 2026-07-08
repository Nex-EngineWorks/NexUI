using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Default command dispatcher: resolves a registered handler by command type and
    /// runs it through the middleware chain (outermost registered runs first).
    /// </summary>
    public sealed class UICommandDispatcher : IUICommandDispatcher
    {
        private readonly List<IUIMiddleware> _middlewares = new List<IUIMiddleware>();
        private readonly Dictionary<Type, Func<IUICommand, UICommandContext, UniTask>> _handlers =
            new Dictionary<Type, Func<IUICommand, UICommandContext, UniTask>>();

        private readonly Func<UICommandContext> _contextFactory;

        /// <summary>Optional command log; when set, every dispatched command is recorded.</summary>
        public Command.CommandLog Log { get; set; }

        /// <summary>Raised after a command's pipeline completes successfully.</summary>
        public event Action<IUICommand> CommandExecuted;

        public UICommandDispatcher(Func<UICommandContext> contextFactory = null)
        {
            _contextFactory = contextFactory ?? (() => new UICommandContext());
        }

        public void UseMiddleware(IUIMiddleware middleware)
        {
            if (middleware != null)
                _middlewares.Add(middleware);
        }

        public void RegisterHandler<TCommand>(IUICommandHandler<TCommand> handler)
            where TCommand : IUICommand
        {
            if (handler == null) return;
            _handlers[typeof(TCommand)] = (cmd, ctx) => handler.HandleAsync((TCommand)cmd, ctx);
        }

        public async UniTask DispatchAsync(IUICommand command)
        {
            if (command == null) return;

            var context = _contextFactory();

            if (!_handlers.TryGetValue(command.GetType(), out var handler))
            {
                Debug.LogWarning($"[NexUI] No handler registered for command '{command.CommandId}' ({command.GetType().Name}).");
                return;
            }

            // Build the middleware pipeline from the inside out.
            Func<UniTask> next = () => handler(command, context);
            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                var mw = _middlewares[i];
                var inner = next;
                next = () => mw.InvokeAsync(command, context, inner);
            }

            await next();

            Log?.Add(command);
            CommandExecuted?.Invoke(command);
        }
    }
}
