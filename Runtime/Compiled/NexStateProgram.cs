using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// One authored state - Normal, Selected, Locked - and the range of deltas it applies.
    /// </summary>
    /// <remarks>
    /// Deltas live in the program's shared list and are addressed by
    /// <see cref="DeltaStart"/> + <see cref="DeltaCount"/>, the same flattening
    /// <see cref="NexInteractionRule"/> uses for its actions: Unity serializes nested collections
    /// poorly, and the flat form keeps a screen's whole state table in two contiguous lists.
    /// </remarks>
    [Serializable]
    public struct NexStateEntry
    {
        public string StateId;

        /// <summary>Author-facing name. Carried so a runtime state picker can show it.</summary>
        public string DisplayName;

        /// <summary>The state the screen starts in when nothing selects one.</summary>
        public bool IsDefault;

        public int DeltaStart;
        public int DeltaCount;
    }

    /// <summary>
    /// Every state a screen declares, resolved against its node table.
    /// </summary>
    /// <remarks>
    /// Applying a state is base + deltas, never state-to-state: switching from Selected to Locked
    /// restores the base first. Diffing the two states directly would be fewer writes, but it makes
    /// the result depend on which state the screen happened to be in, so a screen that reached
    /// Locked by two different routes would not look the same.
    /// </remarks>
    [Serializable]
    public sealed class NexStateProgram
    {
        public List<NexStateEntry> States = new List<NexStateEntry>();
        public List<NexPropertyDelta> Deltas = new List<NexPropertyDelta>();

        public bool IsEmpty => States.Count == 0;

        /// <summary>Index of a state by id, or -1.</summary>
        /// <remarks>
        /// A linear scan: a screen has a handful of states and this runs when one is selected, not
        /// per frame, so a dictionary would cost memory in every shipped build to speed up
        /// something no frame does.
        /// </remarks>
        public int IndexOf(string stateId)
        {
            if (string.IsNullOrEmpty(stateId)) return -1;

            for (int i = 0; i < States.Count; i++)
                if (string.Equals(States[i].StateId, stateId, StringComparison.Ordinal)) return i;

            return -1;
        }

        /// <summary>The default state's index, or -1 when none is marked.</summary>
        public int DefaultIndex()
        {
            for (int i = 0; i < States.Count; i++)
                if (States[i].IsDefault) return i;

            return -1;
        }

        /// <summary>The deltas of one state, in authored order.</summary>
        public IEnumerable<NexPropertyDelta> DeltasFor(int stateIndex)
        {
            if (stateIndex < 0 || stateIndex >= States.Count) yield break;

            var entry = States[stateIndex];
            for (int i = 0; i < entry.DeltaCount; i++)
                yield return Deltas[entry.DeltaStart + i];
        }

        /// <summary>
        /// Every node any state touches.
        /// </summary>
        /// <remarks>
        /// What a backend needs to snapshot before it applies the first state, so that switching
        /// states can restore the base. Snapshotting the whole screen instead would copy hundreds
        /// of nodes to be able to restore three.
        /// </remarks>
        public HashSet<int> AffectedNodes()
        {
            var nodes = new HashSet<int>();
            for (int i = 0; i < Deltas.Count; i++) nodes.Add(Deltas[i].NodeIndex);
            return nodes;
        }
    }
}
