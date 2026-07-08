using System;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core.Command
{
    /// <summary>One recorded command in the <see cref="CommandLog"/>.</summary>
    public readonly struct CommandLogEntry
    {
        public IUICommand Command { get; }
        public string CommandId { get; }
        public DateTime TimestampUtc { get; }
        public int Index { get; }

        public CommandLogEntry(IUICommand command, int index, DateTime timestampUtc)
        {
            Command = command;
            CommandId = command?.CommandId;
            Index = index;
            TimestampUtc = timestampUtc;
        }
    }
}
