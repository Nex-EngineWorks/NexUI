using System;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core.Command
{
    /// <summary>
    /// Re-dispatches recorded commands through a dispatcher to reproduce a session.
    /// Useful for bug repros and deterministic tests.
    /// </summary>
    public sealed class CommandReplay
    {
        private readonly IUICommandDispatcher _dispatcher;

        public CommandReplay(IUICommandDispatcher dispatcher)
            => _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        public Task ReplayAsync(CommandLog log)
            => ReplayUntilAsync(log, log?.Count ?? 0);

        public async Task ReplayUntilAsync(CommandLog log, int exclusiveEndIndex)
        {
            if (log == null) return;
            int end = Math.Min(exclusiveEndIndex, log.Count);
            for (int i = 0; i < end; i++)
            {
                var command = log.Entries[i].Command;
                if (command != null)
                    await _dispatcher.DispatchAsync(command);
            }
        }
    }
}
