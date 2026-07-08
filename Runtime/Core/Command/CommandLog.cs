using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core.Command
{
    /// <summary>
    /// Append-only (with trimming) history of dispatched commands. Enables inspection,
    /// deterministic replay and undo. Deterministic replay relies on each command
    /// carrying enough payload to be re-executed.
    /// </summary>
    public sealed class CommandLog
    {
        private readonly List<CommandLogEntry> _entries = new List<CommandLogEntry>();
        private readonly CommandHistoryOptions _options;
        private int _counter;

        public CommandLog(CommandHistoryOptions options = null)
            => _options = options ?? CommandHistoryOptions.Default;

        public IReadOnlyList<CommandLogEntry> Entries => _entries;
        public int Count => _entries.Count;

        /// <summary>Raised for each recorded command.</summary>
        public event Action<CommandLogEntry> Recorded;

        public void Add(IUICommand command)
        {
            if (command == null) return;

            var entry = new CommandLogEntry(
                command, _counter++,
                _options.RecordTimestamps ? DateTime.UtcNow : default);
            _entries.Add(entry);

            if (_options.MaxEntries > 0 && _entries.Count > _options.MaxEntries)
                _entries.RemoveAt(0);

            Recorded?.Invoke(entry);
        }

        public void Clear()
        {
            _entries.Clear();
            _counter = 0;
        }
    }
}
