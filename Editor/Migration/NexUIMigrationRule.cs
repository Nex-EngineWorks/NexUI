namespace emiteat.NexUI.Editor.Migration
{
    /// <summary>
    /// E1: one textual find/replace rule for a breaking rename between NexUI versions
    /// (namespace, class, or asset-key rename). <see cref="OldToken"/> is matched as a
    /// literal substring, not a regex - renames in this category are exact identifier
    /// swaps (e.g. "Hyojun.NexUI" -&gt; "emiteat.NexUI"), never patterns.
    /// </summary>
    public sealed class NexUIMigrationRule
    {
        public readonly string Id;
        public readonly string OldToken;
        public readonly string NewToken;
        public readonly string Description;
        public readonly string IntroducedInVersion;

        public NexUIMigrationRule(string id, string oldToken, string newToken, string description, string introducedInVersion)
        {
            Id = id;
            OldToken = oldToken;
            NewToken = newToken;
            Description = description;
            IntroducedInVersion = introducedInVersion;
        }
    }
}
