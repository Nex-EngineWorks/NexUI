using System;
using System.Collections.Generic;
using emiteat.NexUI.Diagnostics;

namespace emiteat.NexUI.Interaction
{
    /// <summary>What the author's element handed to the handler when a command fired.</summary>
    public struct NexCommandContext
    {
        /// <summary>The command id authored on the element, e.g. <c>Game.Start</c>.</summary>
        public string CommandId;

        /// <summary>Authoring path of the element that fired it, e.g. <c>MainMenu/StartButton</c>.</summary>
        public string SenderPath;

        /// <summary>Authoring stable id of that element.</summary>
        public string SenderNodeId;

        /// <summary>Screen id the element belongs to.</summary>
        public string ScreenId;
    }

    /// <summary>Outcome of a dispatch, reported rather than thrown.</summary>
    public struct NexCommandResult
    {
        public bool Handled;

        /// <summary>Set when the dispatch produced a problem worth reporting.</summary>
        public NexDiagnostic Diagnostic;

        public static NexCommandResult Ok() => new NexCommandResult { Handled = true };

        public static NexCommandResult Problem(NexDiagnostic diagnostic)
            => new NexCommandResult { Handled = false, Diagnostic = diagnostic };
    }

    /// <summary>
    /// Maps the command ids authored on elements to the game code that runs them.
    /// </summary>
    /// <remarks>
    /// Instance-based, not a singleton: a router belongs to whoever owns the screens - a scene
    /// bootstrap, a DI container, a test - so two of them can coexist and a test never has to
    /// undo global registrations left by the last one.
    ///
    /// The id is a string here because that is what the authoring document stores today. It is
    /// the seam a generated typed registry (<c>UICommands.Game.Start</c>) will sit on top of, so
    /// authors stop typing ids by hand while the runtime keeps one lookup.
    ///
    /// A missing handler is a warning that reaches the caller (NEX-RT-6001), not a silent no-op.
    /// "The button does nothing and nothing is logged" is the single most expensive UI bug to
    /// track down, and it is entirely preventable here.
    /// </remarks>
    public sealed class NexCommandRouter
    {
        private readonly Dictionary<string, Action<NexCommandContext>> _handlers =
            new Dictionary<string, Action<NexCommandContext>>(StringComparer.Ordinal);

        /// <summary>Raised for every diagnostic the router produces, so a host can surface them.</summary>
        public event Action<NexDiagnostic> DiagnosticRaised;

        public int HandlerCount => _handlers.Count;

        /// <summary>
        /// Registers the handler for a command id, replacing any previous one. Returns a handle
        /// that unregisters on dispose, so a screen or a test can clean up without knowing
        /// whether it was the one that registered.
        /// </summary>
        public IDisposable Register(string commandId, Action<NexCommandContext> handler)
        {
            if (string.IsNullOrEmpty(commandId) || handler == null) return Registration.Empty;

            _handlers[commandId] = handler;
            return new Registration(this, commandId, handler);
        }

        public bool IsRegistered(string commandId)
            => !string.IsNullOrEmpty(commandId) && _handlers.ContainsKey(commandId);

        /// <summary>All registered ids, for the runtime debugger's "why did nothing happen?" view.</summary>
        public IEnumerable<string> RegisteredCommandIds => _handlers.Keys;

        public NexCommandResult Dispatch(NexCommandContext context)
        {
            if (string.IsNullOrEmpty(context.CommandId))
                return NexCommandResult.Ok(); // Nothing authored; not a problem.

            var location = new NexSourceLocation(context.ScreenId, context.SenderNodeId, context.SenderPath, "command");

            if (!_handlers.TryGetValue(context.CommandId, out var handler))
            {
                var diagnostic = Raise(NexDiagnosticCodes.Create(
                    NexDiagnosticCodes.NoCommandHandler,
                    location,
                    "No handler is registered for command '" + context.CommandId + "'.",
                    "Fired by " + (context.SenderPath ?? context.SenderNodeId) + "."));
                return NexCommandResult.Problem(diagnostic);
            }

            try
            {
                handler(context);
                return NexCommandResult.Ok();
            }
            catch (Exception ex)
            {
                // Swallowed on purpose, then reported: one throwing handler must not take down
                // the input system or leave the UI in a half-updated state for every later click.
                var diagnostic = Raise(NexDiagnosticCodes.Create(
                    NexDiagnosticCodes.CommandHandlerThrew,
                    location,
                    "Handler for command '" + context.CommandId + "' threw " + ex.GetType().Name + ".",
                    ex.ToString()));
                return NexCommandResult.Problem(diagnostic);
            }
        }

        private NexDiagnostic Raise(NexDiagnostic diagnostic)
        {
            var handler = DiagnosticRaised;
            if (handler != null) handler(diagnostic);
            return diagnostic;
        }

        private sealed class Registration : IDisposable
        {
            public static readonly Registration Empty = new Registration(null, null, null);

            private NexCommandRouter _router;
            private readonly string _commandId;
            private readonly Action<NexCommandContext> _handler;

            public Registration(NexCommandRouter router, string commandId, Action<NexCommandContext> handler)
            {
                _router = router;
                _commandId = commandId;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_router == null) return;

                // Only remove our own registration: a later Register for the same id replaced us,
                // and disposing a stale handle must not silently unhook the live handler.
                if (_router._handlers.TryGetValue(_commandId, out var current) && current == _handler)
                    _router._handlers.Remove(_commandId);

                _router = null;
            }
        }
    }
}
