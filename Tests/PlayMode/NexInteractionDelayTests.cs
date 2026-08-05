using System.Collections.Generic;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.Time;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// Covers rules that pause partway through.
    /// </summary>
    /// <remarks>
    /// A delay turns the interaction engine from something that finishes inside one call into
    /// something that holds state across frames, which brings its own failure modes: work that
    /// resumes after the screen is gone, work that runs twice, work that never runs. Each of those
    /// gets a test here.
    /// </remarks>
    public sealed class NexInteractionDelayTests
    {
        private NexScreenProgram _program;
        private FakeState _state;
        private NexManualTime _time;

        [TearDown]
        public void TearDown()
        {
            if (_program != null) Object.DestroyImmediate(_program);
            _program = null;
        }

        /// <summary>Node 0 is a Button carrying one rule; the actions are supplied per test.</summary>
        private NexInteractionRuntime Build(params NexInteractionAction[] actions)
        {
            var nodes = new[]
            {
                new NexNodeProgram { NodeId = "n-btn", Name = "Button", ParentIndex = -1, Kind = NexNodeKind.Button }
            };

            var sourceMap = new NexSourceMap();
            sourceMap.Add("n-btn", "Button", 0, "Root/Button");

            var interactions = new NexInteractionProgram();
            interactions.Actions.AddRange(actions);
            interactions.Rules.Add(new NexInteractionRule
            {
                RuleId = "rule-1",
                NodeIndex = 0,
                Trigger = NexTrigger.OnClick,
                Phase = NexPhase.Target,
                ActionStart = 0,
                ActionCount = actions.Length
            });

            _program = ScriptableObject.CreateInstance<NexScreenProgram>();
            _program.Initialize("TestScreen", nodes, sourceMap, new NexFeatureManifest(),
                new Vector2(1920f, 1080f), "hash", interactions);

            _state = new FakeState();
            _time = new NexManualTime();
            return new NexInteractionRuntime(_program, new NexCommandRouter(), _state, new NoSurface(), _time);
        }

        private static NexInteractionAction Set(string key)
            => new NexInteractionAction
            {
                Kind = NexActionKind.SetState, StateKey = key, StringValue = "1", TargetNodeIndex = -1
            };

        private static NexInteractionAction Delay(double seconds)
            => new NexInteractionAction { Kind = NexActionKind.Delay, Seconds = seconds, TargetNodeIndex = -1 };

        // ---- sequencing -----------------------------------------------------

        [Test]
        public void Delay_StopsTheRuleUntilTheTimePasses()
        {
            var runtime = Build(Set("before"), Delay(1d), Set("after"));

            runtime.Fire(0, NexTrigger.OnClick);
            Assert.AreEqual(new[] { "before" }, _state.WriteOrder.ToArray());
            Assert.AreEqual(1, runtime.PendingCount);

            _time.Advance(0.5d);
            runtime.Tick();
            Assert.AreEqual(new[] { "before" }, _state.WriteOrder.ToArray(), "Half way is not there yet.");

            _time.Advance(0.6d);
            runtime.Tick();
            Assert.AreEqual(new[] { "before", "after" }, _state.WriteOrder.ToArray());
            Assert.AreEqual(0, runtime.PendingCount);
        }

        [Test]
        public void Delay_SupportsSeveralPausesInOneRule()
        {
            var runtime = Build(Set("a"), Delay(1d), Set("b"), Delay(1d), Set("c"));

            runtime.Fire(0, NexTrigger.OnClick);
            _time.Advance(1.1d);
            runtime.Tick();
            _time.Advance(1.1d);
            runtime.Tick();

            Assert.AreEqual(new[] { "a", "b", "c" }, _state.WriteOrder.ToArray());
        }

        [Test]
        public void Tick_RunsNothingWhileTheDelayIsStillPending()
        {
            var runtime = Build(Set("a"), Delay(10d), Set("b"));
            runtime.Fire(0, NexTrigger.OnClick);

            for (int i = 0; i < 20; i++) runtime.Tick();

            Assert.AreEqual(new[] { "a" }, _state.WriteOrder.ToArray());
        }

        [Test]
        public void Tick_ResumesEachContinuationExactlyOnce()
        {
            var runtime = Build(Set("a"), Delay(1d), Set("b"));
            runtime.Fire(0, NexTrigger.OnClick);

            _time.Advance(5d);
            runtime.Tick();
            runtime.Tick();
            runtime.Tick();

            Assert.AreEqual(new[] { "a", "b" }, _state.WriteOrder.ToArray(),
                "A resumed rule must not run again on the next pump.");
        }

        // ---- concurrency ----------------------------------------------------

        [Test]
        public void Fire_TwiceParksTwoIndependentContinuations()
        {
            var runtime = Build(Set("a"), Delay(1d), Set("b"));

            runtime.Fire(0, NexTrigger.OnClick);
            runtime.Fire(0, NexTrigger.OnClick);
            Assert.AreEqual(2, runtime.PendingCount, "Clicking twice starts the rule twice.");

            _time.Advance(1.1d);
            runtime.Tick();

            Assert.AreEqual(new[] { "a", "a", "b", "b" }, _state.WriteOrder.ToArray());
            Assert.AreEqual(0, runtime.PendingCount);
        }

        [Test]
        public void Tick_ResumesInDeadlineOrder()
        {
            var runtime = Build(Set("a"), Delay(1d), Set("b"));

            runtime.Fire(0, NexTrigger.OnClick);   // due at t=1
            _time.Advance(0.5d);
            runtime.Fire(0, NexTrigger.OnClick);   // due at t=1.5

            _time.Advance(1.1d);                   // now t=1.6, both due
            runtime.Tick();

            Assert.AreEqual(new[] { "a", "a", "b", "b" }, _state.WriteOrder.ToArray());
        }

        // ---- teardown -------------------------------------------------------

        [Test]
        public void CancelPending_StopsWorkThatWouldOutliveTheScreen()
        {
            var runtime = Build(Set("a"), Delay(1d), Set("after-teardown"));
            runtime.Fire(0, NexTrigger.OnClick);

            runtime.CancelPending();
            _time.Advance(10d);
            runtime.Tick();

            Assert.AreEqual(new[] { "a" }, _state.WriteOrder.ToArray(),
                "A delayed action resuming after teardown looks like a bug in the next screen.");
            Assert.AreEqual(0, runtime.PendingCount);
        }

        // ---- cost -----------------------------------------------------------

        [Test]
        public void HasDelays_IsFalseForAScreenWithoutOne()
        {
            var runtime = Build(Set("a"), Set("b"));
            runtime.Fire(0, NexTrigger.OnClick);

            Assert.IsFalse(_program.Interactions.HasDelays(),
                "The backend uses this to decide whether the screen needs a per-frame pump at all.");
            Assert.AreEqual(0, runtime.PendingCount);
        }

        [Test]
        public void HasDelays_IsTrueOnceOneExists()
        {
            Build(Set("a"), Delay(1d), Set("b"));

            Assert.IsTrue(_program.Interactions.HasDelays());
        }

        [Test]
        public void Delay_OfZeroResumesOnTheNextPump()
        {
            var runtime = Build(Set("a"), Delay(0d), Set("b"));

            runtime.Fire(0, NexTrigger.OnClick);
            Assert.AreEqual(new[] { "a" }, _state.WriteOrder.ToArray(),
                "Even a zero delay ends the current call; the rest belongs to the next pump.");

            runtime.Tick();
            Assert.AreEqual(new[] { "a", "b" }, _state.WriteOrder.ToArray());
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
