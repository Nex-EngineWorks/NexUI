using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.MotionClip;
using emiteat.NexUI.MotionGraph;
using emiteat.NexUI.Tests.Fakes;

namespace emiteat.NexUI.Tests.EditMode
{
    public sealed class UIGraphExecutorTests
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

            public UniTask ExecuteAsync(UIGraphNodeExecutionArgs args)
            {
                Started = true;
                return _gate.Task;
            }

            public void Release() => _gate.TrySetResult();
        }

        private static UIMotionGraphAsset MakeGraph(UIGraphNode[] nodes, string startNodeId)
        {
            var graph = ScriptableObject.CreateInstance<UIMotionGraphAsset>();
            graph.nodes = nodes;
            graph.entryPoints = new[] { new UIGraphEntryPoint { eventName = "Start", nodeId = startNodeId } };
            return graph;
        }

        [Test]
        public async Task Sequence_RunsStepsInOrder()
        {
            var log = new List<string>();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "seq", nodeType = "Flow.Sequence",
                    flowOutputs = new[] { new UIGraphFlowOutput("Step0", "a"), new UIGraphFlowOutput("Step1", "b") }
                },
                new UIGraphNode { id = "a", nodeType = "Test.Record" },
                new UIGraphNode { id = "b", nodeType = "Test.Record" }
            }, "seq");

            var executors = new List<IUIGraphNodeExecutor>(BuiltInGraphNodeExecutors.CreateDefaults()) { new RecordNodeExecutor(log) };
            var executor = new UIGraphExecutor(graph, executors);

            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void Parallel_LaunchesAllBranchesBeforeEitherCompletes()
        {
            var branchA = new GatedNodeExecutor("Test.GateA");
            var branchB = new GatedNodeExecutor("Test.GateB");

            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "p", nodeType = "Flow.Parallel",
                    flowOutputs = new[] { new UIGraphFlowOutput("BranchA", "a"), new UIGraphFlowOutput("BranchB", "b") }
                },
                new UIGraphNode { id = "a", nodeType = "Test.GateA" },
                new UIGraphNode { id = "b", nodeType = "Test.GateB" }
            }, "p");

            var executors = new List<IUIGraphNodeExecutor>(BuiltInGraphNodeExecutors.CreateDefaults()) { branchA, branchB };
            var executor = new UIGraphExecutor(graph, executors);

            _ = executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            Assert.IsTrue(branchA.Started);
            Assert.IsTrue(branchB.Started, "Parallel must start both branches before either completes.");

            branchA.Release();
            branchB.Release();
        }

        [Test]
        public async Task Parallel_AnyFinishedCompletesAfterFirstBranch()
        {
            var branchA = new GatedNodeExecutor("Test.GateA");
            var branchB = new GatedNodeExecutor("Test.GateB");
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "p", nodeType = "Flow.Parallel",
                    flowOutputs = new[] { new UIGraphFlowOutput("A", "a"), new UIGraphFlowOutput("B", "b") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource
                        {
                            portName = "Completion Policy", kind = UIGraphPortSourceKind.Constant,
                            constant = UIGraphValue.String("Any Finished")
                        }
                    }
                },
                new UIGraphNode { id = "a", nodeType = "Test.GateA" },
                new UIGraphNode { id = "b", nodeType = "Test.GateB" }
            }, "p");
            var executor = new UIGraphExecutor(graph,
                new List<IUIGraphNodeExecutor>(BuiltInGraphNodeExecutors.CreateDefaults()) { branchA, branchB });

            var run = executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });
            branchA.Release();
            await run;

            Assert.IsTrue(branchB.Started);
            branchB.Release();
        }

        [Test]
        public async Task Parallel_DoNotWaitReturnsAfterLaunchingBranches()
        {
            var branchA = new GatedNodeExecutor("Test.GateA");
            var branchB = new GatedNodeExecutor("Test.GateB");
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "p", nodeType = "Flow.Parallel",
                    flowOutputs = new[] { new UIGraphFlowOutput("A", "a"), new UIGraphFlowOutput("B", "b") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource
                        {
                            portName = "Completion Policy", kind = UIGraphPortSourceKind.Constant,
                            constant = UIGraphValue.String("Do Not Wait")
                        }
                    }
                },
                new UIGraphNode { id = "a", nodeType = "Test.GateA" },
                new UIGraphNode { id = "b", nodeType = "Test.GateB" }
            }, "p");
            var executor = new UIGraphExecutor(graph,
                new List<IUIGraphNodeExecutor>(BuiltInGraphNodeExecutors.CreateDefaults()) { branchA, branchB });

            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            Assert.IsTrue(branchA.Started);
            Assert.IsTrue(branchB.Started);
            branchA.Release();
            branchB.Release();
        }

        [Test]
        public async Task Branch_TrueCondition_FollowsTrueOutput()
        {
            var log = new List<string>();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "branch", nodeType = "Flow.Branch",
                    flowOutputs = new[] { new UIGraphFlowOutput("True", "yes"), new UIGraphFlowOutput("False", "no") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Condition", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Bool(true) }
                    }
                },
                new UIGraphNode { id = "yes", nodeType = "Test.Record" },
                new UIGraphNode { id = "no", nodeType = "Test.Record" }
            }, "branch");

            var executors = new List<IUIGraphNodeExecutor>(BuiltInGraphNodeExecutors.CreateDefaults()) { new RecordNodeExecutor(log) };
            var executor = new UIGraphExecutor(graph, executors);

            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "yes" }, log);
        }

        [Test]
        public async Task Delay_ZeroDuration_ProceedsToNextWithoutWaiting()
        {
            var log = new List<string>();
            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "delay", nodeType = "Flow.Delay",
                    flowOutputs = new[] { new UIGraphFlowOutput("Next", "after") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Duration", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Float(0f) }
                    }
                },
                new UIGraphNode { id = "after", nodeType = "Test.Record" }
            }, "delay");

            var executors = new List<IUIGraphNodeExecutor>(BuiltInGraphNodeExecutors.CreateDefaults()) { new RecordNodeExecutor(log) };
            var executor = new UIGraphExecutor(graph, executors);

            await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });

            CollectionAssert.AreEqual(new[] { "after" }, log);
        }

        [Test]
        public async Task PlayMotionClip_PlaysClipThenContinuesToCompleted()
        {
            var log = new List<string>();
            var player = new FakeMotionClipPlayer();
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();

            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "play", nodeType = "Motion.PlayClip",
                    flowOutputs = new[] { new UIGraphFlowOutput("Completed", "after") },
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Clip", kind = UIGraphPortSourceKind.Constant, constant = UIGraphValue.Clip(clip) }
                    }
                },
                new UIGraphNode { id = "after", nodeType = "Test.Record" }
            }, "play");

            var executors = new List<IUIGraphNodeExecutor>(BuiltInGraphNodeExecutors.CreateDefaults())
            {
                new PlayMotionClipNodeExecutor(player),
                new RecordNodeExecutor(log)
            };
            var executor = new UIGraphExecutor(graph, executors);

            // The synchronous prefix (down through PlayAsync's creation of its pending task) runs
            // before this call returns, so PlayCount is already 1 here - only the "Completed"
            // continuation is actually waiting on the fake player's gate.
            var runTask = executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") });
            Assert.AreEqual(1, player.PlayCount);

            player.CompletePending();
            await runTask;

            CollectionAssert.AreEqual(new[] { "after" }, log);
        }

        [Test]
        public void UnknownNodeType_IsSkippedGracefully()
        {
            var graph = MakeGraph(new[]
            {
                new UIGraphNode { id = "mystery", nodeType = "SomeFutureNodeType" }
            }, "mystery");

            var executor = new UIGraphExecutor(graph);

            Assert.DoesNotThrowAsync(async () =>
                await executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s") }));
        }

        [Test]
        public void MissingEntryPoint_IsANoOp()
        {
            var graph = MakeGraph(new UIGraphNode[0], "does-not-exist");
            graph.entryPoints = new UIGraphEntryPoint[0];
            var executor = new UIGraphExecutor(graph);

            Assert.DoesNotThrowAsync(async () =>
                await executor.RunEventAsync("NoSuchEvent", new UIGraphExecutionContext { Surface = new FakeSurface("s") }));
        }

        [Test]
        public void ResolveInput_CurrentEventTarget_ReturnsContextElementId()
        {
            var log = new List<string>();
            UIGraphValue captured = default;

            var graph = MakeGraph(new[]
            {
                new UIGraphNode
                {
                    id = "capture", nodeType = "Test.Capture",
                    dataInputs = new[]
                    {
                        new UIGraphPortSource { portName = "Target", kind = UIGraphPortSourceKind.CurrentEventTarget }
                    }
                }
            }, "capture");

            IUIGraphNodeExecutor captureExecutor = new CaptureNodeExecutor(v => captured = v);
            var executor = new UIGraphExecutor(graph, new[] { captureExecutor });

            _ = executor.RunEventAsync("Start", new UIGraphExecutionContext { Surface = new FakeSurface("s"), EventTargetElementId = "button1" });

            Assert.AreEqual(UIGraphValueType.Element, captured.type);
            Assert.AreEqual("button1", captured.stringValue);
        }

        private sealed class CaptureNodeExecutor : IUIGraphNodeExecutor
        {
            public string NodeType => "Test.Capture";
            private readonly System.Action<UIGraphValue> _onCapture;
            public CaptureNodeExecutor(System.Action<UIGraphValue> onCapture) => _onCapture = onCapture;

            public UniTask ExecuteAsync(UIGraphNodeExecutionArgs args)
            {
                _onCapture(args.ResolveInput("Target"));
                return UniTask.CompletedTask;
            }
        }
    }
}
