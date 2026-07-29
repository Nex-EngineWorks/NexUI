using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.MotionGraph;
using emiteat.NexUI.Tests.Fakes;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>Phase 6 node set: expressions/Blackboard/Command/Repeat/Race/Subgraph/Runtime Trace. Timeout's real-timer "TimedOut" branch is intentionally not covered here - see the comment on that test group below.</summary>
    public sealed class UIGraphPhase6Tests
    {
        private sealed class RecordNodeExecutor : IUIGraphNodeExecutor
        {
            public string NodeType => "Test.Record";
            private readonly List<string> _log;
            public RecordNodeExecutor(List<string> log) => _log = log;
            public UniTask ExecuteAsync(UIGraphNodeExecutionArgs args)
            {
                _log.Add(args.Node.id);
                return args.RunNext("Next", args.CancellationToken);
            }
        }

        private sealed class GatedNodeExecutor : IUIGraphNodeExecutor
        {
            public string NodeType { get; }
            public bool Started { get; private set; }
            private readonly UniTaskCompletionSource _gate = new UniTaskCompletionSource();
            public GatedNodeExecutor(string nodeType) => NodeType = nodeType;
            public UniTask ExecuteAsync(UIGraphNodeExecutionArgs args) { Started = true; return _gate.Task; }
            public void Release() => _gate.TrySetResult();
        }

        private static UIMotionGraphAsset MakeGraph(UIGraphNode[] nodes, string startNodeId)
        {
            var graph = ScriptableObject.CreateInstance<UIMotionGraphAsset>();
            graph.nodes = nodes;
            graph.entryPoints = new[] { new UIGraphEntryPoint { eventName = "Start", nodeId = startNodeId } };
            return graph;
        }

        private static List<IUIGraphNodeExecutor> Defaults(params IUIGraphNodeExecutor[] extra)
        {
            var list = new List<IUIGraphNodeExecutor>(BuiltInGraphNodeExecutors.CreateDefaults());
            list.AddRange(extra);
            return list;
        }

        // ---- Expression + NodeOutput wiring -----------------------------------------------

        [TestCase("Add", 2f, 3f, 5f)]
        [TestCase("Subtract", 5f, 3f, 2f)]
        [TestCase("Multiply", 4f, 3f, 12f)]
        [TestCase("Divide", 9f, 3f, 3f)]
        public async Task Expression_Arithmetic_ComputesResult(string op, float a, float b, float expected)
        {
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "expr", nodeType = "Data.Expression",
                    flowOutputs = new[] { new UIGraphFlowOutput("Next", null) },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Operation", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String(op) },
                        new UIGraphPortSource { portName = "A", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Float(a) },
                        new UIGraphPortSource { portName = "B", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Float(b) }
                    }
                }
            }, "expr");

            var executor = new UIGraphExecutor(graph, Defaults());
            var context = new UIGraphExecutionContext { Surface = new FakeSurface("s") };
            await executor.RunEventAsync("Start", context);

            Assert.IsTrue(context.TryGetNodeOutput("expr", "Result", out var result));
            Assert.AreEqual(expected, result.floatValue, 0.0001f);
        }

        [Test]
        public async Task Expression_GreaterThan_FeedsBranchConditionViaNodeOutput()
        {
            var log = new List<string>();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "expr", nodeType = "Data.Expression",
                    flowOutputs = new[] { new UIGraphFlowOutput("Next", "branch") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Operation", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String("GreaterThan") },
                        new UIGraphPortSource { portName = "A", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Float(10f) },
                        new UIGraphPortSource { portName = "B", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Float(5f) }
                    }
                },
                new UIGraphNode
                {
                    id = "branch", nodeType = "Flow.Branch",
                    flowOutputs = new[] { new UIGraphFlowOutput("True", "yes"), new UIGraphFlowOutput("False", "no") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Condition", kind = UIGraphPortSourceKind.NodeOutput, sourceNodeId = "expr", sourceOutputName = "Result" }
                    }
                },
                new UIGraphNode { id = "yes", nodeType = "Test.Record" },
                new UIGraphNode { id = "no", nodeType = "Test.Record" }
            }, "expr");

            var executor = new UIGraphExecutor(graph, Defaults(new RecordNodeExecutor(log)));
            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "yes" }, log);
        }

        // ---- Blackboard: Set Variable + Variable source -----------------------------------

        [Test]
        public async Task SetFloatVariable_ThenReadingItAsVariableSource_ReturnsWrittenValue()
        {
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "set", nodeType = "Data.SetFloatVariable",
                    flowOutputs = new[] { new UIGraphFlowOutput("Next", "expr") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Name", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String("health") },
                        new UIGraphPortSource { portName = "Value", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Float(42f) }
                    }
                },
                new UIGraphNode
                {
                    id = "expr", nodeType = "Data.Expression",
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Operation", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String("Add") },
                        new UIGraphPortSource { portName = "A", kind = UIGraphPortSourceKind.Variable, name = "health" },
                        new UIGraphPortSource { portName = "B", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Float(0f) }
                    }
                }
            }, "set");

            var executor = new UIGraphExecutor(graph, Defaults());
            var context = new UIGraphExecutionContext { Surface = new FakeSurface("s") };
            await executor.RunEventAsync("Start", context);

            Assert.AreEqual(42f, context.Variables["health"].floatValue, 0.0001f);
            Assert.IsTrue(context.TryGetNodeOutput("expr", "Result", out var result));
            Assert.AreEqual(42f, result.floatValue, 0.0001f);
        }

        // ---- Command.Dispatch ---------------------------------------------------------------

        [Test]
        public async Task DispatchCommand_Success_BundlesPayloadAndContinuesToSuccess()
        {
            var log = new List<string>();
            var dispatcher = new FakeCommandDispatcher();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "cmd", nodeType = "Command.Dispatch",
                    flowOutputs = new[] { new UIGraphFlowOutput("Success", "ok"), new UIGraphFlowOutput("Failed", "fail") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "CommandId", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String("UseItem") },
                        new UIGraphPortSource { portName = "Payload.slotId", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String("slot3") }
                    }
                },
                new UIGraphNode { id = "ok", nodeType = "Test.Record" },
                new UIGraphNode { id = "fail", nodeType = "Test.Record" }
            }, "cmd");

            var executor = new UIGraphExecutor(graph, Defaults(new RecordNodeExecutor(log)));
            var context = new UIGraphExecutionContext { Surface = new FakeSurface("s"), CommandDispatcher = dispatcher };
            await executor.RunEventAsync("Start", context);

            CollectionAssert.AreEqual(new[] { "ok" }, log);
            Assert.AreEqual(1, dispatcher.Dispatched.Count);
            var command = (UIGraphCommand)dispatcher.Dispatched[0];
            Assert.AreEqual("UseItem", command.CommandId);
            Assert.AreEqual("slot3", command.Payload["slotId"].stringValue);
        }

        [Test]
        public async Task DispatchCommand_NoDispatcherWiredUp_ContinuesToFailed()
        {
            var log = new List<string>();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "cmd", nodeType = "Command.Dispatch",
                    flowOutputs = new[] { new UIGraphFlowOutput("Success", "ok"), new UIGraphFlowOutput("Failed", "fail") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "CommandId", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String("UseItem") }
                    }
                },
                new UIGraphNode { id = "ok", nodeType = "Test.Record" },
                new UIGraphNode { id = "fail", nodeType = "Test.Record" }
            }, "cmd");

            var executor = new UIGraphExecutor(graph, Defaults(new RecordNodeExecutor(log)));
            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "fail" }, log);
        }

        [Test]
        public async Task DispatchCommand_DispatcherThrows_ContinuesToFailed()
        {
            var log = new List<string>();
            var dispatcher = new FakeCommandDispatcher { ThrowOnDispatch = true };
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "cmd", nodeType = "Command.Dispatch",
                    flowOutputs = new[] { new UIGraphFlowOutput("Success", "ok"), new UIGraphFlowOutput("Failed", "fail") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "CommandId", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String("UseItem") }
                    }
                },
                new UIGraphNode { id = "ok", nodeType = "Test.Record" },
                new UIGraphNode { id = "fail", nodeType = "Test.Record" }
            }, "cmd");

            var executor = new UIGraphExecutor(graph, Defaults(new RecordNodeExecutor(log)));
            var context = new UIGraphExecutionContext { Surface = new FakeSurface("s"), CommandDispatcher = dispatcher };
            await executor.RunEventAsync("Start", context);

            CollectionAssert.AreEqual(new[] { "fail" }, log);
        }

        // ---- Repeat -----------------------------------------------------------------------

        [Test]
        public async Task Repeat_RunsBodyExactlyCountTimesThenCompletes()
        {
            var log = new List<string>();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "repeat", nodeType = "Flow.Repeat",
                    flowOutputs = new[] { new UIGraphFlowOutput("Body", "body"), new UIGraphFlowOutput("Completed", "done") },
                    dataInputs = new[] { new UIGraphPortSource { portName = "Count", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Int(3) } }
                },
                new UIGraphNode { id = "body", nodeType = "Test.Record" },
                new UIGraphNode { id = "done", nodeType = "Test.Record" }
            }, "repeat");

            var executor = new UIGraphExecutor(graph, Defaults(new RecordNodeExecutor(log)));
            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "body", "body", "body", "done" }, log);
        }

        // ---- Race (fully deterministic - no real timers involved) --------------------------

        [Test]
        public async Task Race_FirstBranchToFinishWins_ContinuesToCompleted()
        {
            var log = new List<string>();
            var slowBranch = new GatedNodeExecutor("Test.Slow");

            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "race", nodeType = "Flow.Race",
                    flowOutputs = new[] { new UIGraphFlowOutput("Step0", "fast"), new UIGraphFlowOutput("Step1", "slow"), new UIGraphFlowOutput("Completed", "done") }
                },
                new UIGraphNode { id = "fast", nodeType = "Test.Record" },
                new UIGraphNode { id = "slow", nodeType = "Test.Slow" },
                new UIGraphNode { id = "done", nodeType = "Test.Record" }
            }, "race");

            var executor = new UIGraphExecutor(graph, Defaults(new RecordNodeExecutor(log), slowBranch));

            // Race only waits for the FIRST branch to finish (the instantly-completing "fast" one) -
            // it never awaits "slow" again after that, so this completes without "slow" ever
            // releasing its gate; that's the behavior under test, not a hang risk.
            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            Assert.IsTrue(slowBranch.Started, "Race must launch every branch, not just the eventual winner.");
            CollectionAssert.AreEqual(new[] { "fast", "done" }, log);
        }

        // ---- Timeout (only the "Body wins" path - see class doc comment) -------------------

        [Test]
        public async Task Timeout_BodyFinishesWell_BeforeGenerousDuration_ContinuesToCompleted()
        {
            var log = new List<string>();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "timeout", nodeType = "Flow.Timeout",
                    flowOutputs = new[] { new UIGraphFlowOutput("Body", "body"), new UIGraphFlowOutput("Completed", "done"), new UIGraphFlowOutput("TimedOut", "timedout") },
                    dataInputs = new[] { new UIGraphPortSource { portName = "Duration", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Float(30f) } }
                },
                new UIGraphNode { id = "body", nodeType = "Test.Record" },
                new UIGraphNode { id = "done", nodeType = "Test.Record" },
                new UIGraphNode { id = "timedout", nodeType = "Test.Record" }
            }, "timeout");

            var executor = new UIGraphExecutor(graph, Defaults(new RecordNodeExecutor(log)));
            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "body", "done" }, log);
        }

        // ---- Subgraph ----------------------------------------------------------------------

        [Test]
        public async Task RunSubgraph_ExecutesInnerGraphsEventThenContinuesToCompleted()
        {
            var log = new List<string>();
            var innerGraph = MakeGraph(new[]
            {
                new UIGraphNode { id = "innerEvent", nodeType = "Event", flowOutputs = new[] { new UIGraphFlowOutput("Next", "innerRecord") } },
                new UIGraphNode { id = "innerRecord", nodeType = "Test.Record" }
            }, "innerEvent");
            innerGraph.entryPoints = new[] { new UIGraphEntryPoint { eventName = "InnerStart", nodeId = "innerEvent" } };

            var outerGraph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "sub", nodeType = "Graph.RunSubgraph",
                    flowOutputs = new[] { new UIGraphFlowOutput("Completed", "outerDone") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Graph", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Graph(innerGraph) },
                        new UIGraphPortSource { portName = "EventName", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.String("InnerStart") }
                    }
                },
                new UIGraphNode { id = "outerDone", nodeType = "Test.Record" }
            }, "sub");

            var executor = new UIGraphExecutor(outerGraph, Defaults(new RecordNodeExecutor(log)));
            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "innerRecord", "outerDone" }, log);
        }

        // ---- Runtime Trace ------------------------------------------------------------------

        [Test]
        public async Task RuntimeTrace_NodeStartedAndCompleted_FireForEveryExecutedNode()
        {
            var started = new List<string>();
            var completed = new List<string>();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode { id = "a", nodeType = "Test.Record", flowOutputs = new[] { new UIGraphFlowOutput("Next", "b") } },
                new UIGraphNode { id = "b", nodeType = "Test.Record" }
            }, "a");

            var executor = new UIGraphExecutor(graph, Defaults(new RecordNodeExecutor(new List<string>())));
            executor.NodeStarted += n => started.Add(n.id);
            executor.NodeCompleted += (n, elapsed) => completed.Add(n.id);

            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "a", "b" }, started);
            CollectionAssert.AreEqual(new[] { "b", "a" }, completed,
                "A parent node completes after the downstream node it awaited.");
        }

        [Test]
        public void RuntimeTrace_NodeFailed_FiresWhenAnExecutorThrows()
        {
            var failed = new List<string>();

            var throwingExecutor = new ThrowingNodeExecutor();
            var graph = MakeGraph(new[] { new UIGraphNode { id = "boom", nodeType = "Test.Throw" } }, "boom");
            var executor = new UIGraphExecutor(graph, Defaults(throwingExecutor));
            executor.NodeFailed += (n, ex) => failed.Add(n.id);

            Assert.CatchAsync<System.Exception>(async () =>
                await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") }));

            CollectionAssert.AreEqual(new[] { "boom" }, failed);
        }

        private sealed class ThrowingNodeExecutor : IUIGraphNodeExecutor
        {
            public string NodeType => "Test.Throw";
            public UniTask ExecuteAsync(UIGraphNodeExecutionArgs args) => throw new System.InvalidOperationException("boom");
        }
    }
}
