using System;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Authoring: optional node-graph representation of motion. This is the data model
    /// only; the Motion Graph Editor UI is a Designer concern and is intentionally not
    /// implemented here. The compiler flattens a graph's nodes into sequential steps.
    /// </summary>
    [Serializable]
    public sealed class UIMotionGraph
    {
        [Serializable]
        public sealed class Node
        {
            public string id;
            public UIMotionStep step;
            /// <summary>Ids of nodes that must complete before this node starts.</summary>
            public string[] dependencies = Array.Empty<string>();
        }

        public Node[] nodes = Array.Empty<Node>();

        public bool HasContent => nodes != null && nodes.Length > 0;
    }
}
