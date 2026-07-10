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

            // One state object per dispatch instead of one closure per middleware (B4: audited
            // for per-call allocations - a dispatch with N middlewares previously allocated N
            // closure environments here; this allocates exactly one).
            var state = new DispatchState(command, context, handler, _middlewares);
            await state.InvokeNext();

            Log?.Add(command);
            CommandExecuted?.Invoke(command);
        }

        /// <summary>Walks the middleware chain by index instead of pre-building N nested closures per dispatch.</summary>
        private sealed class DispatchState
        {
            private readonly IUICommand _command;
            private readonly UICommandContext _context;
            private readonly Func<IUICommand, UICommandContext, UniTask> _handler;
            private readonly List<IUIMiddleware> _middlewares;
            private int _index;

            public DispatchState(IUICommand command, UICommandContext context,
                Func<IUICommand, UICommandContext, UniTask> handler, List<IUIMiddleware> middlewares)
            {
                _command = command;
                _context = context;
                _handler = handler;
                _middlewares = middlewares;
            }

            public UniTask InvokeNext()
            {
                if (_index >= _middlewares.Count)
                    return _handler(_command, _context);

                var mw = _middlewares[_index++];
                return mw.InvokeAsync(_command, _context, InvokeNext);
            }
        }
    }
}
