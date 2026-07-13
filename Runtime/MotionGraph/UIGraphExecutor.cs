using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace emiteat.NexUI.MotionGraph
{
    /// <summary>
    /// Interprets a <see cref="UIMotionGraphAsset"/>: starting from an entry point, walks nodes via
    /// their <see cref="IUIGraphNodeExecutor"/>, letting each node decide which of its own flow
    /// outputs to continue to (and how - Sequence walks them in order, Parallel launches them all
    /// concurrently). Unknown node types are skipped, not thrown on, so a graph referencing a node
    /// type from a newer Designer version degrades gracefully instead of crashing a shipped game.
    /// </summary>
    public sealed class UIGraphExecutor
    {
        private readonly UIMotionGraphAsset _graph;
        private readonly Dictionary<string, IUIGraphNodeExecutor> _executors = new Dictionary<string, IUIGraphNodeExecutor>();

        /// <summary>Runtime Trace hooks (brief §25/§37): a future Debug workspace subscribes to these instead of the executor needing any Editor dependency.</summary>
        public event Action<UIGraphNode> NodeStarted;
        public event Action<UIGraphNode, TimeSpan> NodeCompleted;
        public event Action<UIGraphNode, Exception> NodeFailed;

        public UIGraphExecutor(UIMotionGraphAsset graph, IEnumerable<IUIGraphNodeExecutor> executors = null)
        {
            _graph = graph;
            foreach (var executor in executors ?? BuiltInGraphNodeExecutors.CreateDefaults())
                _executors[executor.NodeType] = executor;
        }

        public UniTask RunEventAsync(string eventName, UIGraphExecutionContext context, CancellationToken cancellationToken = default)
        {
            var entryNodeId = _graph != null ? _graph.FindEntryNodeId(eventName) : null;
            return RunNodeAsync(entryNodeId, context, cancellationToken);
        }

        public UniTask RunFromNodeAsync(string nodeId, UIGraphExecutionContext context, CancellationToken cancellationToken = default)
            => RunNodeAsync(nodeId, context, cancellationToken);

        private async UniTask RunNodeAsync(string nodeId, UIGraphExecutionContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(nodeId) || cancellationToken.IsCancellationRequested || _graph == null) return;

            var node = _graph.FindNode(nodeId);
            if (node == null) return;
            if (!_executors.TryGetValue(node.nodeType, out var executor)) return;

            var args = new UIGraphNodeExecutionArgs
            {
                Node = node,
                Context = context,
                CancellationToken = cancellationToken,
                ResolveInput = portName => ResolveInput(node, portName, context),
                RunNext = (outputName, ct) => RunNodeAsync(node.FindFlowTarget(outputName), context, ct),
                CreateSubExecutor = subgraph => new UIGraphExecutor(subgraph, _executors.Values)
            };

            NodeStarted?.Invoke(node);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await executor.ExecuteAsync(args);
                NodeCompleted?.Invoke(node, stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                NodeFailed?.Invoke(node, ex);
                throw;
            }
        }

        private UIGraphValue ResolveInput(UIGraphNode node, string portName, UIGraphExecutionContext context)
        {
            var source = node.FindInput(portName);
            if (source == null) return default;

            switch (source.kind)
            {
                case UIGraphPortSourceKind.CurrentEventTarget:
                    return UIGraphValue.Element(context.EventTargetElementId);

                case UIGraphPortSourceKind.NodeOutput:
                    return context.TryGetNodeOutput(source.sourceNodeId, source.sourceOutputName, out var nodeOutput) ? nodeOutput : default;

                case UIGraphPortSourceKind.Parameter:
                    return context.Parameters.TryGetValue(source.name, out var parameter) ? parameter : default;

                case UIGraphPortSourceKind.Variable:
                    return context.Variables.TryGetValue(source.name, out var variable) ? variable : default;

                case UIGraphPortSourceKind.BuiltInDeltaTime:
                    return UIGraphValue.Float(UnityEngine.Time.deltaTime);

                case UIGraphPortSourceKind.BuiltInUnscaledDeltaTime:
                    return UIGraphValue.Float(UnityEngine.Time.unscaledDeltaTime);

                case UIGraphPortSourceKind.Constant:
                default:
                    return source.constant;
            }
        }
    }
}
