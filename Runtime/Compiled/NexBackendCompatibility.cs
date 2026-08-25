using System.Collections.Generic;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// One capability a screen uses that a backend cannot honour, and where it is used.
    /// </summary>
    /// <remarks>
    /// Counted per node and named by the first one, rather than listed exhaustively. An inventory
    /// grid where forty slots share a corner radius has one problem, not forty, and a report that
    /// prints forty rows makes the author scroll past the row that matters.
    /// </remarks>
    public struct NexCompatibilityGap
    {
        public NexCapability Capability;

        /// <summary>How many nodes on this screen ask for it.</summary>
        public int NodeCount;

        /// <summary>Authoring path of the first node that does, so the author can go and look.</summary>
        public string FirstNodePath;
    }

    /// <summary>
    /// What a compiled screen will lose on a given backend, worked out before it ships.
    /// </summary>
    /// <remarks>
    /// A pure pass over the compiled program and the capability table, deliberately separate from
    /// both the compiler and the backends. The compiler stays backend-neutral - it has no business
    /// knowing what uGUI can draw - and the backends keep reporting at build time for the cases
    /// only they can see. This answers the question neither of them can: "what will this screen
    /// lose, on each backend, before I pick one?"
    ///
    /// Running it for every backend at once is the point. A per-backend check answers "is this
    /// screen fine on uGUI"; a matrix answers "which backend should this screen ship on", which is
    /// the decision the author is actually making.
    /// </remarks>
    public static class NexBackendCompatibility
    {
        /// <summary>Gaps between what this screen uses and what one backend can do.</summary>
        public static List<NexCompatibilityGap> Analyze(NexScreenProgram program, NexBackendId backend)
        {
            var gaps = new List<NexCompatibilityGap>();
            if (program == null || program.Nodes == null) return gaps;

            // Insertion-ordered by first use rather than by enum value: the author reads this
            // top-down against their own screen, and document order is the order they built it in.
            var index = new Dictionary<NexCapability, int>();
            var used = new List<NexCapability>();
            var nodes = program.Nodes;

            for (int i = 0; i < nodes.Length; i++)
            {
                used.Clear();
                NexCapabilityUse.Collect(nodes[i], used);

                for (int u = 0; u < used.Count; u++)
                {
                    var capability = used[u];
                    if (NexBackendCapabilities.Supports(backend, capability)) continue;

                    if (index.TryGetValue(capability, out var at))
                    {
                        var existing = gaps[at];
                        existing.NodeCount++;
                        gaps[at] = existing;
                        continue;
                    }

                    index[capability] = gaps.Count;
                    gaps.Add(new NexCompatibilityGap
                    {
                        Capability = capability,
                        NodeCount = 1,
                        FirstNodePath = PathOf(program, i)
                    });
                }
            }

            return gaps;
        }

        /// <summary>Gaps for every backend the capability table describes.</summary>
        public static Dictionary<NexBackendId, List<NexCompatibilityGap>> AnalyzeAll(NexScreenProgram program)
        {
            var byBackend = new Dictionary<NexBackendId, List<NexCompatibilityGap>>();
            foreach (var backend in NexBackendCapabilities.Backends)
                byBackend[backend] = Analyze(program, backend);

            return byBackend;
        }

        /// <summary>
        /// True when this screen is honoured completely by at least one backend.
        /// </summary>
        /// <remarks>
        /// The useful summary line, and deliberately not "no backend has any gap". A screen that
        /// ships on one backend does not care what the other one cannot draw; a screen that no
        /// backend can render as authored is the one worth stopping for.
        /// </remarks>
        public static bool AnyBackendIsComplete(NexScreenProgram program)
        {
            foreach (var backend in NexBackendCapabilities.Backends)
                if (Analyze(program, backend).Count == 0) return true;

            return false;
        }

        private static string PathOf(NexScreenProgram program, int nodeIndex)
        {
            var path = program.SourceMap != null ? program.SourceMap.PathOfIndex(nodeIndex) : null;
            if (!string.IsNullOrEmpty(path)) return path;

            return nodeIndex >= 0 && nodeIndex < program.Nodes.Length
                ? program.Nodes[nodeIndex].Name
                : string.Empty;
        }
    }
}
