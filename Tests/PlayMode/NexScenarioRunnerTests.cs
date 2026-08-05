using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Scenario;
using NUnit.Framework;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// Runs whole scenarios against a fake world - no GameObjects, no canvas, no frame loop.
    /// </summary>
    /// <remarks>
    /// That this is possible at all is the point of <see cref="INexScenarioWorld"/>. A failure here
    /// is the runner's fault and never the uGUI backend's, which is what makes these tests worth
    /// reading when a real scenario misbehaves.
    /// </remarks>
    public sealed class NexScenarioRunnerTests
    {
        private FakeWorld _world;

        [SetUp]
        public void SetUp() => _world = new FakeWorld();

        private NexScenarioResult Run(NexScenario scenario)
            => new NexScenarioRunner(scenario, _world).RunToCompletion();

        // ---- the happy path -------------------------------------------------

        [Test]
        public void Run_ExecutesAWholeJourney()
        {
            _world.Add("store.item.purchase", visible: true);
            _world.OnClick = _ => _world.SetState("Player.Currency", 500);

            var result = Run(NexScenario.Named("PurchaseItem")
                .Find("store.item.purchase")
                .AssertVisible()
                .Click()
                .AssertState("Player.Currency", NexComparison.Equals, "500")
                .AssertNoErrors());

            Assert.IsTrue(result.Succeeded, result.ToString());
            Assert.IsTrue(result.Steps.All(s => s.Status == NexScenarioStepStatus.Passed));
        }

        [Test]
        public void Run_ReportsEveryStepInOrder()
        {
            _world.Add("a");

            var result = Run(NexScenario.Named("S").Find("a").Click());

            Assert.AreEqual(2, result.Steps.Count);
            Assert.AreEqual(NexScenarioStepKind.Find, result.Steps[0].Step.Kind);
            Assert.AreEqual(NexScenarioStepKind.Click, result.Steps[1].Step.Kind);
        }

        // ---- failures -------------------------------------------------------

        [Test]
        public void Run_FailsWhenTheAutomationIdIsNotOnTheScreen()
        {
            var result = Run(NexScenario.Named("S").Find("nope").Click());

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(NexDiagnosticCodes.ScenarioElementNotFound, result.Failure.Code);
        }

        [Test]
        public void Run_StopsAtTheFirstFailureAndMarksTheRestNotRun()
        {
            _world.Add("a");

            var result = Run(NexScenario.Named("S")
                .Find("missing")
                .Click()
                .AssertVisible());

            Assert.AreEqual(NexScenarioStepStatus.Failed, result.Steps[0].Status);
            Assert.AreEqual(NexScenarioStepStatus.NotRun, result.Steps[1].Status);
            Assert.AreEqual(NexScenarioStepStatus.NotRun, result.Steps[2].Status);
            Assert.AreEqual(0, _world.Clicks, "Nothing after the failure may actually run.");
        }

        [Test]
        public void Run_FailsWhenAStepActsBeforeAnythingWasFound()
        {
            var result = Run(NexScenario.Named("S").Click());

            Assert.AreEqual(NexDiagnosticCodes.ScenarioNoTarget, result.Failure.Code);
        }

        [Test]
        public void Run_FailsAVisibilityAssertionWithWhatItActuallySaw()
        {
            _world.Add("a", visible: false);

            var result = Run(NexScenario.Named("S").Find("a").AssertVisible());

            Assert.AreEqual(NexDiagnosticCodes.ScenarioAssertionFailed, result.Failure.Code);
            StringAssert.Contains("visible", result.Steps[1].Detail);
        }

        [Test]
        public void Run_FailsATextAssertionWithBothValues()
        {
            _world.Add("a", text: "Buy");

            var result = Run(NexScenario.Named("S").Find("a").AssertText("Purchase"));

            StringAssert.Contains("Purchase", result.Steps[1].Detail);
            StringAssert.Contains("Buy", result.Steps[1].Detail);
        }

        [Test]
        public void Run_FailsAStateAssertionWhenTheKeyWasNeverSet()
        {
            var result = Run(NexScenario.Named("S")
                .AssertState("Never.Set", NexComparison.Equals, "1"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("<unset>", result.Steps[0].Detail);
        }

        // ---- waiting --------------------------------------------------------

        [Test]
        public void WaitUntil_PassesOnceTheConditionBecomesTrue()
        {
            _world.SetState("Store.IsPurchasing", true);

            var runner = new NexScenarioRunner(
                NexScenario.Named("S").WaitUntil("Store.IsPurchasing", NexComparison.Equals, "false"),
                _world);

            // Three polls of "still busy", then the work finishes.
            for (int i = 0; i < 3; i++) Assert.IsTrue(runner.MoveNext());
            _world.SetState("Store.IsPurchasing", false);
            runner.RunToCompletion();

            Assert.IsTrue(runner.Result.Succeeded, runner.Result.ToString());
            Assert.AreEqual(4, runner.Result.Steps[0].Polls);
        }

        [Test]
        public void WaitUntil_FailsWhenTheConditionNeverHolds()
        {
            _world.SetState("Store.IsPurchasing", true);

            var result = Run(NexScenario.Named("S")
                .WaitUntil("Store.IsPurchasing", NexComparison.Equals, "false", pollBudget: 5));

            Assert.AreEqual(NexDiagnosticCodes.ScenarioTimedOut, result.Failure.Code);
            Assert.AreEqual(5, result.Steps[0].Polls);
        }

        [Test]
        public void WaitUntil_ComparesBooleansWrittenAsText()
        {
            // The state holds a real bool; the scenario is written with "false". Without the
            // boolean path in NexValueComparison this fails, because bool.ToString() is "False".
            _world.SetState("Ready", false);

            var result = Run(NexScenario.Named("S")
                .WaitUntil("Ready", NexComparison.Equals, "false", pollBudget: 2));

            Assert.IsTrue(result.Succeeded, result.ToString());
        }

        // ---- diagnostics ----------------------------------------------------

        [Test]
        public void AssertNoErrors_FailsWhenTheScreenRaisedOne()
        {
            _world.Raise(NexSeverity.Error, "boom");

            var result = Run(NexScenario.Named("S").AssertNoErrors());

            Assert.AreEqual(NexDiagnosticCodes.ScenarioReportedDiagnostics, result.Failure.Code);
        }

        [Test]
        public void AssertNoErrors_IgnoresWarnings()
        {
            _world.Raise(NexSeverity.Warning, "just a warning");

            Assert.IsTrue(Run(NexScenario.Named("S").AssertNoErrors()).Succeeded,
                "A scenario asked for no errors, not for a silent screen.");
        }

        // ---- reporting ------------------------------------------------------

        [Test]
        public void Report_NamesTheScenarioAndTheFailingStep()
        {
            var text = Run(NexScenario.Named("PurchaseItem").Find("missing")).ToString();

            StringAssert.Contains("PurchaseItem", text);
            StringAssert.Contains("Find(missing)", text);
            StringAssert.Contains("✗", text);
        }

        // ---- fake -----------------------------------------------------------

        private sealed class FakeWorld : INexScenarioWorld
        {
            private readonly Dictionary<string, int> _byAutomationId = new Dictionary<string, int>();
            private readonly List<bool> _visible = new List<bool>();
            private readonly List<string> _text = new List<string>();
            private readonly Dictionary<string, object> _state = new Dictionary<string, object>();
            private readonly List<NexDiagnostic> _diagnostics = new List<NexDiagnostic>();

            public int Clicks { get; private set; }
            public System.Action<int> OnClick { get; set; }

            public IReadOnlyList<NexDiagnostic> Diagnostics => _diagnostics;

            public void Add(string automationId, bool visible = true, string text = "")
            {
                _byAutomationId[automationId] = _visible.Count;
                _visible.Add(visible);
                _text.Add(text);
            }

            public void Raise(NexSeverity severity, string message)
                => _diagnostics.Add(new NexDiagnostic("NEX-TEST-0000", severity, message));

            public bool TryFind(string automationId, out int nodeIndex)
            {
                if (!string.IsNullOrEmpty(automationId) && _byAutomationId.TryGetValue(automationId, out nodeIndex))
                    return true;

                nodeIndex = -1;
                return false;
            }

            public void Click(int nodeIndex)
            {
                Clicks++;
                OnClick?.Invoke(nodeIndex);
            }

            public bool IsVisible(int nodeIndex) => _visible[nodeIndex];

            public string GetText(int nodeIndex) => _text[nodeIndex];

            public bool TryGetState(string key, out object value) => _state.TryGetValue(key, out value);

            public void SetState(string key, object value) => _state[key] = value;
        }
    }
}
