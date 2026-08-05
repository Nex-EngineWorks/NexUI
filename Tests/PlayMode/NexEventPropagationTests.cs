using System.Collections.Generic;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// Covers how a click travels through the hierarchy: which rules see it, in what order, and
    /// when it stops.
    /// </summary>
    /// <remarks>
    /// The screen is Root → Panel → Button, so every phase has somewhere to sit. Each rule writes
    /// its own name into the state store, which makes the order the event actually took directly
    /// assertable rather than inferred.
    /// </remarks>
    public sealed class NexEventPropagationTests
    {
        private const int Root = 0;
        private const int Panel = 1;
        private const int Button = 2;

        private NexScreenProgram _program;
        private FakeState _state;

        [TearDown]
        public void TearDown()
        {
            if (_program != null) Object.DestroyImmediate(_program);
            _program = null;
        }

        // ---- helpers --------------------------------------------------------

        private NexInteractionRuntime Build(params NexInteractionRule[] rules)
        {
            var nodes = new[]
            {
                new NexNodeProgram { NodeId = "n-root", Name = "Root", ParentIndex = -1, Kind = NexNodeKind.Panel },
                new NexNodeProgram { NodeId = "n-panel", Name = "Panel", ParentIndex = Root, Kind = NexNodeKind.Panel },
                new NexNodeProgram { NodeId = "n-btn", Name = "Button", ParentIndex = Panel, Kind = NexNodeKind.Button }
            };

            var sourceMap = new NexSourceMap();
            sourceMap.Add("n-root", "Root", Root, "Root");
            sourceMap.Add("n-panel", "Panel", Panel, "Root/Panel");
            sourceMap.Add("n-btn", "Button", Button, "Root/Panel/Button");

            var interactions = new NexInteractionProgram();
            foreach (var rule in rules)
            {
                var copy = rule;
                copy.ActionStart = interactions.Actions.Count;
                copy.ActionCount = 1;

                // Each rule's only action records that it ran, in order.
                interactions.Actions.Add(new NexInteractionAction
                {
                    Kind = NexActionKind.SetState,
                    StateKey = copy.RuleId,
                    StringValue = "ran",
                    TargetNodeIndex = -1
                });
                interactions.Rules.Add(copy);
            }

            _program = ScriptableObject.CreateInstance<NexScreenProgram>();
            _program.Initialize("TestScreen", nodes, sourceMap, new NexFeatureManifest(),
                new Vector2(1920f, 1080f), "hash", interactions);

            _state = new FakeState();
            return new NexInteractionRuntime(_program, new NexCommandRouter(), _state, new NoSurface());
        }

        private static NexInteractionRule Rule(string id, int node, NexPhase phase, bool stops = false)
            => new NexInteractionRule
            {
                RuleId = id,
                NodeIndex = node,
                Trigger = NexTrigger.OnClick,
                Phase = phase,
                StopsPropagation = stops
            };

        // ---- default behaviour ----------------------------------------------

        [Test]
        public void Fire_WithOnlyTargetRules_BehavesExactlyAsBefore()
        {
            var runtime = Build(
                Rule("root", Root, NexPhase.Target),
                Rule("button", Button, NexPhase.Target));

            runtime.Fire(Button, NexTrigger.OnClick);

            Assert.AreEqual(new[] { "button" }, _state.WriteOrder.ToArray(),
                "A target-phase rule on an ancestor must not react to a descendant's click.");
        }

        // ---- order ----------------------------------------------------------

        [Test]
        public void Fire_RunsCaptureOutermostFirstThenTargetThenBubbleInnermostFirst()
        {
            var runtime = Build(
                Rule("capture-root", Root, NexPhase.Capture),
                Rule("capture-panel", Panel, NexPhase.Capture),
                Rule("target", Button, NexPhase.Target),
                Rule("bubble-panel", Panel, NexPhase.Bubble),
                Rule("bubble-root", Root, NexPhase.Bubble));

            runtime.Fire(Button, NexTrigger.OnClick);

            Assert.AreEqual(
                new[] { "capture-root", "capture-panel", "target", "bubble-panel", "bubble-root" },
                _state.WriteOrder.ToArray());
        }

        [Test]
        public void Fire_BubblesToAnAncestorEvenWhenTheTargetHasNoRule()
        {
            // The case the whole feature exists for: a list reacting to any item being clicked.
            var runtime = Build(Rule("list", Panel, NexPhase.Bubble));

            runtime.Fire(Button, NexTrigger.OnClick);

            Assert.AreEqual(new[] { "list" }, _state.WriteOrder.ToArray());
        }

        [Test]
        public void Fire_DoesNotDeliverToNodesOutsideTheAncestorPath()
        {
            var runtime = Build(Rule("button-bubble", Button, NexPhase.Bubble));

            runtime.Fire(Button, NexTrigger.OnClick);

            Assert.IsEmpty(_state.WriteOrder,
                "A node's own Bubble rule must not fire for its own click - it is not its own ancestor.");
        }

        // ---- stopping -------------------------------------------------------

        [Test]
        public void StopPropagation_OnTheTargetKeepsAncestorsFromSeeingIt()
        {
            var runtime = Build(
                Rule("target", Button, NexPhase.Target, stops: true),
                Rule("bubble-panel", Panel, NexPhase.Bubble),
                Rule("bubble-root", Root, NexPhase.Bubble));

            runtime.Fire(Button, NexTrigger.OnClick);

            Assert.AreEqual(new[] { "target" }, _state.WriteOrder.ToArray());
        }

        [Test]
        public void StopPropagation_DuringCapturePreventsTheTargetFromRunning()
        {
            var runtime = Build(
                Rule("capture-root", Root, NexPhase.Capture, stops: true),
                Rule("target", Button, NexPhase.Target));

            runtime.Fire(Button, NexTrigger.OnClick);

            Assert.AreEqual(new[] { "capture-root" }, _state.WriteOrder.ToArray());
        }

        [Test]
        public void StopPropagation_StopsAtTheInnermostAncestorThatClaimsIt()
        {
            var runtime = Build(
                Rule("bubble-panel", Panel, NexPhase.Bubble, stops: true),
                Rule("bubble-root", Root, NexPhase.Bubble));

            runtime.Fire(Button, NexTrigger.OnClick);

            Assert.AreEqual(new[] { "bubble-panel" }, _state.WriteOrder.ToArray());
        }

        // ---- lifecycle triggers ---------------------------------------------

        [Test]
        public void Fire_DoesNotPropagateLifecycleTriggers()
        {
            var runtime = Build(new NexInteractionRule
            {
                RuleId = "bubble-panel",
                NodeIndex = Panel,
                Trigger = NexTrigger.OnShow,
                Phase = NexPhase.Bubble
            });

            runtime.Fire(Button, NexTrigger.OnShow);

            Assert.IsEmpty(_state.WriteOrder,
                "OnShow belongs to the node that raised it; bubbling it would fire an ancestor " +
                "once per descendant that appeared.");
        }

        // ---- backend wiring --------------------------------------------------

        [Test]
        public void WantsClickListener_AsksEveryNodeOnceAnyRulePropagates()
        {
            var runtime = Build(Rule("list", Panel, NexPhase.Bubble));

            Assert.IsTrue(runtime.WantsClickListener(Button),
                "The node that must report the click is the button, not the panel that cares about it.");
        }

        [Test]
        public void WantsClickListener_StaysNarrowWhenNothingPropagates()
        {
            var runtime = Build(Rule("button", Button, NexPhase.Target));

            Assert.IsTrue(runtime.WantsClickListener(Button));
            Assert.IsFalse(runtime.WantsClickListener(Panel),
                "A screen with no propagating rule must not wire listeners it never uses.");
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

        private sealed class NoSurface : INexScreenSurface
        {
            public void SetVisible(int nodeIndex, bool visible) { }
            public void SetText(int nodeIndex, string text) { }
        }
    }
}
