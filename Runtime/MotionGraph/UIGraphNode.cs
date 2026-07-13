using System;
using UnityEngine;

namespace emiteat.NexUI.MotionGraph
{
    /// <summary>One flow (execution-order) connection out of a node, named so multi-output nodes (Branch's True/False, Sequence's StepN) can be told apart.</summary>
    [Serializable]
    public struct UIGraphFlowOutput
    {
        public string name;
        public string targetNodeId;

        public UIGraphFlowOutput(string name, string targetNodeId)
        {
            this.name = name;
            this.targetNodeId = targetNodeId;
        }
    }

    /// <summary>Where a node's data (typed) input port gets its value from (brief §17's Blackboard: Constant / Parameter / Variable / Node Output / built-ins).</summary>
    public enum UIGraphPortSourceKind
    {
        Constant,

        /// <summary>The element id of whatever triggered graph execution (brief's "Current Event Target").</summary>
        CurrentEventTarget,

        /// <summary>Another node's named output (e.g. <c>Data.Expression</c>'s "Result"), read via <see cref="UIGraphExecutionContext.TryGetNodeOutput"/>.</summary>
        NodeOutput,

        /// <summary>An external input supplied by whatever started this run (<see cref="UIGraphExecutionContext.Parameters"/>), named by <see cref="UIGraphPortSource.name"/>.</summary>
        Parameter,

        /// <summary>A named local scoped to this run (<see cref="UIGraphExecutionContext.Variables"/>), written by <c>Data.SetFloatVariable</c>/<c>Data.SetBoolVariable</c> and named by <see cref="UIGraphPortSource.name"/>.</summary>
        Variable,

        /// <summary><see cref="UnityEngine.Time.deltaTime"/> at resolve time.</summary>
        BuiltInDeltaTime,

        /// <summary><see cref="UnityEngine.Time.unscaledDeltaTime"/> at resolve time.</summary>
        BuiltInUnscaledDeltaTime
    }

    [Serializable]
    public sealed class UIGraphPortSource
    {
        public string portName;
        public UIGraphPortSourceKind kind;
        public UIGraphValue constant;
        public string sourceNodeId;
        public string sourceOutputName;

        /// <summary>Parameter/Variable name - unused by the other source kinds.</summary>
        public string name;
    }

    /// <summary>One node in a <see cref="UIMotionGraphAsset"/>: a type key (resolved against the executor registry), its flow outputs, and its data input bindings.</summary>
    [Serializable]
    public sealed class UIGraphNode
    {
        public string id;
        public string nodeType;

        /// <summary>Editor-only layout hint; never read by <see cref="UIGraphExecutor"/>.</summary>
        public Vector2 position;

        public UIGraphFlowOutput[] flowOutputs = Array.Empty<UIGraphFlowOutput>();
        public UIGraphPortSource[] dataInputs = Array.Empty<UIGraphPortSource>();

        public string FindFlowTarget(string outputName)
        {
            foreach (var output in flowOutputs)
                if (output.name == outputName)
                    return output.targetNodeId;
            return null;
        }

        public UIGraphPortSource FindInput(string portName)
        {
            foreach (var input in dataInputs)
                if (input.portName == portName)
                    return input;
            return null;
        }
    }
}
