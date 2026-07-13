using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.MotionGraph
{
    /// <summary>
    /// Per-run state threaded through a <see cref="UIGraphExecutor"/> execution: which surface the
    /// graph is acting on, the element that triggered it, the Blackboard (brief §17 - Parameters
    /// supplied by the caller, Variables set via <c>Data.SetFloatVariable</c>/<c>SetBoolVariable</c>,
    /// and per-node outputs kept in a separate scope so a user-chosen variable name can never
    /// collide with a "{nodeId}.{outputName}" key), and the command dispatcher <c>Command.Dispatch</c>
    /// sends through (null is a valid "no dispatcher wired up" state - the node just fails cleanly).
    /// </summary>
    public sealed class UIGraphExecutionContext
    {
        public IUISurface Surface;
        public string EventTargetElementId;
        public IUICommandDispatcher CommandDispatcher;

        public readonly Dictionary<string, UIGraphValue> Parameters = new Dictionary<string, UIGraphValue>();
        public readonly Dictionary<string, UIGraphValue> Variables = new Dictionary<string, UIGraphValue>();
        private readonly Dictionary<string, UIGraphValue> _nodeOutputs = new Dictionary<string, UIGraphValue>();

        public void SetNodeOutput(string nodeId, string outputName, UIGraphValue value)
            => _nodeOutputs[$"{nodeId}.{outputName}"] = value;

        public bool TryGetNodeOutput(string nodeId, string outputName, out UIGraphValue value)
            => _nodeOutputs.TryGetValue($"{nodeId}.{outputName}", out value);
    }
}
