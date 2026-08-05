using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// One authoring-to-compiled correspondence: which authoring element produced which node.
    /// </summary>
    [Serializable]
    public struct NexSourceMapEntry
    {
        /// <summary>Authoring <c>stableId</c>.</summary>
        public string NodeId;

        /// <summary>Authoring element id at compile time; may be stale after a rename.</summary>
        public string AuthoringElementId;

        /// <summary>Index into <see cref="NexScreenProgram.Nodes"/>.</summary>
        public int NodeIndex;

        /// <summary>Slash-separated authoring hierarchy path, for reports and error messages.</summary>
        public string NodePath;
    }

    /// <summary>
    /// The authoring half of the source map: compiled node index &lt;-&gt; authoring element.
    /// </summary>
    /// <remarks>
    /// This is the load-bearing piece the whole diagnostics story rests on. Without it a runtime
    /// problem can only be described in terms of the compiled output ("node 47 is null"), which
    /// is useless to the person who authored the screen. With it, every runtime observation can
    /// be phrased against the thing they actually edited.
    ///
    /// The runtime half - compiled node to live GameObject - is <see cref="NexRuntimeSourceMap"/>,
    /// built during screen instantiation. Keeping the two halves separate means the compiled
    /// asset stays immutable and shareable across screen instances.
    /// </remarks>
    [Serializable]
    public sealed class NexSourceMap
    {
        public List<NexSourceMapEntry> Entries = new List<NexSourceMapEntry>();

        [NonSerialized] private Dictionary<string, int> _byNodeId;

        public void Add(string nodeId, string authoringElementId, int nodeIndex, string nodePath)
        {
            Entries.Add(new NexSourceMapEntry
            {
                NodeId = nodeId ?? string.Empty,
                AuthoringElementId = authoringElementId ?? string.Empty,
                NodeIndex = nodeIndex,
                NodePath = nodePath ?? string.Empty
            });
            _byNodeId = null;
        }

        /// <summary>Compiled node index for an authoring id, or -1.</summary>
        public int IndexOf(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return -1;
            EnsureIndex();
            return _byNodeId.TryGetValue(nodeId, out var index) ? index : -1;
        }

        public bool TryGetEntry(string nodeId, out NexSourceMapEntry entry)
        {
            var index = IndexOf(nodeId);
            if (index < 0)
            {
                entry = default;
                return false;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].NodeIndex != index) continue;
                entry = Entries[i];
                return true;
            }

            entry = default;
            return false;
        }

        /// <summary>Authoring path for a compiled node index, or an empty string.</summary>
        public string PathOfIndex(int nodeIndex)
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].NodeIndex == nodeIndex) return Entries[i].NodePath;
            return string.Empty;
        }

        private void EnsureIndex()
        {
            if (_byNodeId != null) return;

            _byNodeId = new Dictionary<string, int>(Entries.Count);
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (!string.IsNullOrEmpty(entry.NodeId))
                    _byNodeId[entry.NodeId] = entry.NodeIndex;
            }
        }
    }
}
