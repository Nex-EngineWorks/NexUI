namespace emiteat.NexUI.Core.Command
{
    /// <summary>Configuration for <see cref="CommandLog"/> retention.</summary>
    public sealed class CommandHistoryOptions
    {
        /// <summary>Maximum retained entries; oldest are trimmed. 0 = unbounded.</summary>
        public int MaxEntries { get; set; } = 512;

        /// <summary>Whether to stamp entries with a UTC timestamp.</summary>
        public bool RecordTimestamps { get; set; } = true;

        public static CommandHistoryOptions Default => new CommandHistoryOptions();
    }
}
