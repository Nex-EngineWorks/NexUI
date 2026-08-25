using System.Collections.Generic;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// A point-in-time capture of every open screen, ordered bottom → top by layer then id.
    /// <see cref="UIManager.RestoreStackAsync"/> replays it: close everything, reopen in order,
    /// letting the normal lifecycle re-push back/modal stacks from each screen's own policy.
    ///
    /// Payloads are stored by reference - this is an in-process session resume, not a
    /// serialization format. Across domain reloads only ids and variants survive.
    /// </summary>
    public sealed class UIScreenStackSnapshot
    {
        public sealed class Entry
        {
            public string ScreenId;
            public UIOpenArgs Args;
        }

        public readonly List<Entry> Entries = new List<Entry>();
    }
}
