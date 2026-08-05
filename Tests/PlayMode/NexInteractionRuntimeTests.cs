using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Flow;
using emiteat.NexUI.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// Runs the interaction engine against fake ports, so a failure here is the engine's fault
    /// and never the uGUI backend's.
    /// </summary>
    /// <remarks>
    /// The engine was designed around exactly these two ports for this reason: a screen's
    /// behaviour can be executed and asserted with no GameObjects, no canvas and no frame loop.
    /// </remarks>
    public sealed class NexInteractionRuntimeTests
    {
        private NexScreenProgram _program;
        private FakeState _state;
        private FakeSurface _surface;
        private NexCommandRouter _router;
        private NexFlowMemorySink _sink;

        [SetUp]
        public void SetUp()
        {
            _state = new FakeState();
            _surface = new FakeSurface();
            _router = new NexCommandRouter();

            _sink = new NexFlowMemorySink();
            NexFlowTrace.ClearSinks();
            NexFlowTrace.AddSink(_sink);
            NexFlowTrace.Level = NexFlowLevel.Full;
        }

        [TearDown]
        public void TearDown()
        {
            NexFlowTrace.Level = NexFlowLevel.Off;
            NexFlowTrace.ClearSinks();

            if (_program != null) Object.DestroyImmediate(_program);
            _program = null;
        }

        // ---- helpers --------------------------------------------------------

        /// <summary>Node 0 is a Button, node 1 a Label - the shape every test here needs.</summary>
        private NexInteractionRuntime Build(NexInteractionRule rule, params NexInteractionAction[] actions)
        {
            var nodes = new[]
            {
                new NexNodeProgram { NodeId = "n-btn", Name = "StartButton", ParentIndex = -1, Kind = NexNodeKind.Button, Visible = true },
                new NexNodeProgram { NodeId = "n-title", Name = "Title", ParentIndex = -1, Kind = NexNodeKind.Label, Visible = true }
            };

            var sourceMap = new NexSourceMap();
            sourceMap.Add("n-btn", "StartButton", 0, "Root/StartButton");
            sourceMap.Add("n-title", "Title", 1, "Root/Title");

            var interactions = new NexInteractionProgram();
            rule.ActionStart = 0;
            rule.ActionCount = actions.Length;
            interactions.Rules.Add(rule);
            interactions.Actions.AddRange(actions);

            _program = ScriptableObject.CreateInstance<NexScreenProgram>();
            _program.Initialize("TestScreen", nodes, sourceMap, new NexFeatureManifest(),
                new Vector2(1920f, 1080f), "hash", interactions);

            return new NexInteractionRuntime(_program, _router, _state, _surface);
        }

        private static NexInteractionRule ClickRule() => new NexInteractionRule
        {
            RuleId = "rule-1",
            NodeIndex = 0,
            Trigger = NexTrigger.OnClick
        };

        // ---- actions --------------------------------------------------------

        [Test]
        public void Fire_RunsEveryActionOfTheRuleInOrder()
        {
            var runtime = Build(ClickRule(),
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "a", StringValue = "1", TargetNodeIndex = -1 },
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "b", StringValue = "2", TargetNodeIndex = -1 });

            runtime.Fire(0, NexTrigger.OnClick);

            Assert.AreEqual(new[] { "a", "b" }, _state.WriteOrder.ToArray());
        }

        [Test]
        public void Fire_DispatchesCommandWithTheAuthoringPathOfTheSender()
        {
            NexCommandContext seen = default;
            _router.Register("Game.Start", ctx => seen = ctx);

            var runtime = Build(ClickRule(),
                new NexInteractionAction { Kind = NexActionKind.ExecuteCommand, CommandId = "Game.Start", TargetNodeIndex = -1 });

            runtime.Fire(0, NexTrigger.OnClick);

            Assert.AreEqual("Game.Start", seen.CommandId);
            Assert.AreEqual("Root/StartButton", seen.SenderPath);
            Assert.AreEqual("n-btn", seen.SenderNodeId);
            Assert.AreEqual("TestScreen", seen.ScreenId);
        }

        [Test]
        public void Fire_SetsStateNumericallyWhenTheAuthoredValueWasNumeric()
        {
            var runtime = Build(ClickRule(),
                new NexInteractionAction
                {
                    Kind = NexActionKind.SetState, StateKey = "Player.Gold",
                    StringValue = "125.5", NumberValue = 125.5d, IsNumeric = true, TargetNodeIndex = -1
                });

            runtime.Fire(0, NexTrigger.OnClick);

            Assert.IsTrue(_state.TryGet("Player.Gold", out var value));
            Assert.IsInstanceOf<double>(value, "A numeric authored value must not reach the store as a string.");
            Assert.AreEqual(125.5d, (double)value, 0.0001d);
        }

        [Test]
        public void Fire_AppliesSurfaceActionsToTheResolvedNode()
        {
            var runtime = Build(ClickRule(),
                new NexInteractionAction { Kind = NexActionKind.SetVisible, TargetNodeIndex = 1, BoolValue = false },
                new NexInteractionAction { Kind = NexActionKind.SetText, TargetNodeIndex = 1, StringValue = "Done" });

            runtime.Fire(0, NexTrigger.OnClick);

            Assert.AreEqual(false, _surface.Visible[1]);
            Assert.AreEqual("Done", _surface.Text[1]);
        }

        // ---- conditions -----------------------------------------------------

        [Test]
        public void Fire_RunsActionsWhenTheConditionPasses()
        {
            var rule = ClickRule();
            rule.HasCondition = true;
            rule.ConditionKey = "Player.Level";
            rule.Comparison = NexComparison.GreaterThan;
            rule.ConditionNumber = 5d;
            rule.ConditionIsNumeric = true;

            _state.Set("Player.Level", 10);

            var runtime = Build(rule,
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "ok", StringValue = "1", TargetNodeIndex = -1 });
            runtime.Fire(0, NexTrigger.OnClick);

            Assert.IsTrue(_state.TryGet("ok", out _));
        }

        [Test]
        public void Fire_SkipsActionsWhenTheConditionFails()
        {
            var rule = ClickRule();
            rule.HasCondition = true;
            rule.ConditionKey = "Player.Level";
            rule.Comparison = NexComparison.GreaterThan;
            rule.ConditionNumber = 50d;
            rule.ConditionIsNumeric = true;

            _state.Set("Player.Level", 10);

            var runtime = Build(rule,
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "ok", StringValue = "1", TargetNodeIndex = -1 });
            runtime.Fire(0, NexTrigger.OnClick);

            Assert.IsFalse(_state.TryGet("ok", out _));

            // The skip must be visible in the trace: "the condition was false" is the answer to
            // the most common interaction bug there is.
            var record = _sink.Records.Last();
            Assert.IsTrue(record.Steps.Any(s => s.Status == NexFlowStatus.Skipped));
        }

        [Test]
        public void Fire_ComparesTextWhenTheAuthoredValueIsNotNumeric()
        {
            var rule = ClickRule();
            rule.HasCondition = true;
            rule.ConditionKey = "Menu.Mode";
            rule.Comparison = NexComparison.Equals;
            rule.ConditionString = "Ready";

            _state.Set("Menu.Mode", "Ready");

            var runtime = Build(rule,
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "ok", StringValue = "1", TargetNodeIndex = -1 });
            runtime.Fire(0, NexTrigger.OnClick);

            Assert.IsTrue(_state.TryGet("ok", out _));
        }

        [Test]
        public void Fire_TreatsAMissingStateKeyAsNotMatching()
        {
            var rule = ClickRule();
            rule.HasCondition = true;
            rule.ConditionKey = "Never.Set";
            rule.Comparison = NexComparison.Equals;
            rule.ConditionString = "Ready";

            var runtime = Build(rule,
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "ok", StringValue = "1", TargetNodeIndex = -1 });
            runtime.Fire(0, NexTrigger.OnClick);

            Assert.IsFalse(_state.TryGet("ok", out _));
        }

        // ---- triggers -------------------------------------------------------

        [Test]
        public void Fire_IgnoresRulesForOtherTriggers()
        {
            var runtime = Build(ClickRule(),
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "ok", StringValue = "1", TargetNodeIndex = -1 });

            runtime.Fire(0, NexTrigger.OnShow);

            Assert.IsFalse(_state.TryGet("ok", out _));
        }

        [Test]
        public void FireAll_RunsANodeOnceEvenWithSeveralRules()
        {
            var runtime = Build(new NexInteractionRule { RuleId = "r", NodeIndex = 0, Trigger = NexTrigger.OnShow },
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "count", StringValue = "1", TargetNodeIndex = -1 });

            // A second rule on the same node and trigger; the node must still fire once.
            var second = new NexInteractionRule
            {
                RuleId = "r2", NodeIndex = 0, Trigger = NexTrigger.OnShow, ActionStart = 0, ActionCount = 1
            };
            _program.Interactions.Rules.Add(second);

            runtime.FireAll(NexTrigger.OnShow);

            Assert.AreEqual(2, _state.WriteOrder.Count,
                "Two rules on one node should run once each - not once per rule per rule.");
        }

        // ---- failure handling -----------------------------------------------

        [Test]
        public void Fire_ReportsAndContinuesWhenACommandHasNoHandler()
        {
            NexDiagnostic raised = null;
            var runtime = Build(ClickRule(),
                new NexInteractionAction { Kind = NexActionKind.ExecuteCommand, CommandId = "Missing", TargetNodeIndex = -1 },
                new NexInteractionAction { Kind = NexActionKind.SetState, StateKey = "after", StringValue = "1", TargetNodeIndex = -1 });
            _router.DiagnosticRaised += d => raised = d;

            runtime.Fire(0, NexTrigger.OnClick);

            Assert.AreEqual(NexDiagnosticCodes.NoCommandHandler, raised?.Code);
            Assert.IsTrue(_state.TryGet("after", out _),
                "One failed action must not cancel the rest of the rule.");
        }

        [Test]
        public void Fire_RecordsTheWholeChainInOneTrace()
        {
            _router.Register("Game.Start", _ => { });

            var rule = ClickRule();
            rule.HasCondition = true;
            rule.ConditionKey = "Menu.Mode";
            rule.Comparison = NexComparison.Equals;
            rule.ConditionString = "Ready";
            _state.Set("Menu.Mode", "Ready");

            var runtime = Build(rule,
                new NexInteractionAction { Kind = NexActionKind.ExecuteCommand, CommandId = "Game.Start", TargetNodeIndex = -1 });
            runtime.Fire(0, NexTrigger.OnClick);

            var record = _sink.Records.Last();
            Assert.AreEqual("TestScreen/Root/StartButton", record.Origin);
            Assert.IsTrue(record.Succeeded);

            var actions = record.Steps.Select(s => s.Action).ToArray();
            Assert.IsTrue(actions.Any(a => a.StartsWith("Trigger.OnClick")));
            Assert.IsTrue(actions.Any(a => a.StartsWith("Menu.Mode")));
            CollectionAssert.Contains(actions, "Dispatch");
            CollectionAssert.Contains(actions, "Invoke");
        }

        [Test]
        public void Fire_OnAScreenWithNoRules_DoesNothingAndAllocatesNoTrace()
        {
            var empty = ScriptableObject.CreateInstance<NexScreenProgram>();
            empty.Initialize("Empty", new NexNodeProgram[0], new NexSourceMap(),
                new NexFeatureManifest(), new Vector2(1920f, 1080f), "hash");

            var runtime = new NexInteractionRuntime(empty, _router, _state, _surface);
            Assert.IsTrue(runtime.IsEmpty);

            runtime.Fire(0, NexTrigger.OnClick);
            Assert.AreEqual(0, _sink.Count, "An empty screen must not produce trace records.");

            Object.DestroyImmediate(empty);
        }

        // ---- fakes ----------------------------------------------------------

        private sealed class FakeState : INexStateAccess
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

            public List<string> WriteOrder { get; } = new List<string>();

            public bool TryGet(string key, out object value) => _values.TryGetValue(key, out value);

            public void Set(string key, object value)
            {
                _values[key] = value;
                WriteOrder.Add(key);
            }
        }

        private sealed class FakeSurface : INexScreenSurface
        {
            public Dictionary<int, bool> Visible { get; } = new Dictionary<int, bool>();
            public Dictionary<int, string> Text { get; } = new Dictionary<int, string>();

            public void SetVisible(int nodeIndex, bool visible) => Visible[nodeIndex] = visible;

            public void SetText(int nodeIndex, string text) => Text[nodeIndex] = text;
        }
    }
}
