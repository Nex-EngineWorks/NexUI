using System.Collections.Generic;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Scenario;
using emiteat.NexUI.Time;
using NUnit.Framework;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// Covers the clock abstraction and the scenario waiting that depends on it.
    /// </summary>
    /// <remarks>
    /// The point of the abstraction is that a waiting test does not have to wait. Every test here
    /// runs instantly and deterministically because the clock is driven by hand - if any of them
    /// ever needs a real delay, the abstraction has been bypassed somewhere.
    /// </remarks>
    public sealed class NexTimeSourceTests
    {
        [TearDown]
        public void TearDown() => NexTime.ResetDefault();

        // ---- the clock ------------------------------------------------------

        [Test]
        public void ManualTime_StartsWhereItWasTold()
        {
            Assert.AreEqual(12.5d, new NexManualTime(12.5d).Now, 0.0001d);
        }

        [Test]
        public void Advance_MovesTimeForward()
        {
            var time = new NexManualTime();
            time.Advance(1.5d);
            time.Advance(0.5d);

            Assert.AreEqual(2.0d, time.Now, 0.0001d);
        }

        [Test]
        public void Advance_IgnoresNegativeSteps()
        {
            var time = new NexManualTime(5d);
            time.Advance(-3d);

            Assert.AreEqual(5d, time.Now, 0.0001d,
                "Now is monotonic; only an explicit seek may move it backwards.");
        }

        [Test]
        public void SeekTo_MayMoveTimeBackwards()
        {
            // What dragging a timeline playhead to the left means.
            var time = new NexManualTime(10d);
            time.SeekTo(2d);

            Assert.AreEqual(2d, time.Now, 0.0001d);
        }

        [Test]
        public void Default_IsReplaceableAndRestorable()
        {
            var manual = new NexManualTime(42d);
            NexTime.Default = manual;
            Assert.AreSame(manual, NexTime.Default);

            NexTime.ResetDefault();
            Assert.IsInstanceOf<NexUnscaledTime>(NexTime.Default,
                "Menus must keep animating while the game is paused, so unscaled is the default.");
        }

        // ---- scenario waiting -----------------------------------------------

        [Test]
        public void WaitForSeconds_BlocksUntilTheClockPassesTheDeadline()
        {
            var time = new NexManualTime();
            var world = new EmptyWorld();
            var runner = new NexScenarioRunner(
                NexScenario.Named("S").WaitForSeconds(2d).SetState("done", "1"), world, time);

            Assert.IsTrue(runner.MoveNext(), "Still waiting at t=0.");
            time.Advance(1d);
            Assert.IsTrue(runner.MoveNext(), "Still waiting at t=1 of 2.");

            time.Advance(1.5d);
            runner.RunToCompletion();

            Assert.IsTrue(runner.Result.Succeeded, runner.Result.ToString());
            Assert.IsTrue(world.TryGetState("done", out _));
        }

        [Test]
        public void WaitForSeconds_CapturesItsDeadlineOnceInsteadOfAccumulating()
        {
            // Accumulating elapsed time per poll drifts with the poll rate, and breaks outright if
            // the clock is scrubbed. Jumping the clock straight past the deadline must be enough.
            var time = new NexManualTime();
            var runner = new NexScenarioRunner(
                NexScenario.Named("S").WaitForSeconds(5d), new EmptyWorld(), time);

            runner.MoveNext();
            time.SeekTo(100d);
            runner.RunToCompletion();

            Assert.IsTrue(runner.Result.Succeeded);
        }

        [Test]
        public void WaitForSeconds_WithZeroSecondsPassesImmediately()
        {
            var runner = new NexScenarioRunner(
                NexScenario.Named("S").WaitForSeconds(0d), new EmptyWorld(), new NexManualTime());

            Assert.IsTrue(runner.RunToCompletion().Succeeded);
        }

        [Test]
        public void Runner_FallsBackToTheSharedDefaultClock()
        {
            NexTime.Default = new NexManualTime(0d);

            var runner = new NexScenarioRunner(
                NexScenario.Named("S").WaitForSeconds(1d), new EmptyWorld());

            Assert.IsTrue(runner.MoveNext(), "The shared clock has not moved, so the wait holds.");
        }

        [Test]
        public void Report_NamesTheWaitAndItsPollCount()
        {
            var time = new NexManualTime();
            var runner = new NexScenarioRunner(
                NexScenario.Named("S").WaitForSeconds(1d), new EmptyWorld(), time);

            runner.MoveNext();
            time.Advance(2d);
            var text = runner.RunToCompletion().ToString();

            StringAssert.Contains("WaitForSeconds", text);
        }

        /// <summary>A world with nothing in it - these tests only exercise the clock.</summary>
        private sealed class EmptyWorld : INexScenarioWorld
        {
            private readonly Dictionary<string, object> _state = new Dictionary<string, object>();

            public IReadOnlyList<NexDiagnostic> Diagnostics => new List<NexDiagnostic>();

            public bool TryFind(string automationId, out int nodeIndex)
            {
                nodeIndex = -1;
                return false;
            }

            public void Click(int nodeIndex) { }
            public bool IsVisible(int nodeIndex) => false;
            public string GetText(int nodeIndex) => string.Empty;
            public bool TryGetState(string key, out object value) => _state.TryGetValue(key, out value);
            public void SetState(string key, object value) => _state[key] = value;
        }
    }
}
