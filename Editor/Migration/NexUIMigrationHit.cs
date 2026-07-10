using System.Collections.Generic;

namespace emiteat.NexUI.Editor.Migration
{
    /// <summary>One file that matched one or more <see cref="NexUIMigrationRule"/>s, with a per-rule occurrence count.</summary>
    public sealed class NexUIMigrationHit
    {
        public readonly string AssetPath;
        public readonly string AbsolutePath;
        public readonly Dictionary<NexUIMigrationRule, int> OccurrencesByRule = new Dictionary<NexUIMigrationRule, int>();
        public bool Selected = true;

        public NexUIMigrationHit(string assetPath, string absolutePath)
        {
            AssetPath = assetPath;
            AbsolutePath = absolutePath;
        }

        public int TotalOccurrences
        {
            get
            {
                var total = 0;
                foreach (var count in OccurrencesByRule.Values) total += count;
                return total;
            }
        }
    }
}
