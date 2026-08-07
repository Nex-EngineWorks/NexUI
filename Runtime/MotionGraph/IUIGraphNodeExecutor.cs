using System;
using System.Threading;
using System.Threading.Tasks;

namespace emiteat.NexUI.MotionGraph
{
    /// <summary>Continues execution to a node's named flow output (e.g. "Next", "True", "Step0"). A no-op if that output isn't wired to a target node.</summary>
    public delegate Task UIGraphFlowRunner(string outputName, CancellationToken cancellationToken);

    /// <summary>Everything a node executor needs, bundled so the interface doesn't grow a parameter per feature (same rationale as <c>MotionClipTimelineContext</c>).</summary>
    public sealed class UIGraphNodeExecutionArgs
    {
        public UIGraphNode Node;
        public UIGraphExecutionContext Context;
        public UIGraphFlowRunner RunNext;
        public Func<string, UIGraphValue> ResolveInput;
        public CancellationToken CancellationToken;

        /// <summary>
        /// Builds a new <see cref="UIGraphExecutor"/> for a different <see cref="UIMotionGraphAsset"/>
        /// that knows every node type the current executor does (<c>Graph.RunSubgraph</c> uses this
        /// so a project's custom-registered node executors work inside subgraphs too, instead of the
        /// subgraph silently falling back to only the built-in set).
        /// </summary>
        public Func<UIMotionGraphAsset, UIGraphExecutor> CreateSubExecutor;
    }

    /// <summary>
    /// One node type's behavior. Registered into <see cref="UIGraphExecutor"/> by <see cref="NodeType"/>
    /// key (matches <c>DesignerComponentRegistry</c>'s descriptor-over-switch-statement pattern) so
    /// new node types are additive, not edits to a growing switch.
    /// </summary>
    public interface IUIGraphNodeExecutor
    {
        string NodeType { get; }
        Task ExecuteAsync(UIGraphNodeExecutionArgs args);
    }
}
