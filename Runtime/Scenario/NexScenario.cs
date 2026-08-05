using System.Collections.Generic;
using emiteat.NexUI.Compiled;

namespace emiteat.NexUI.Scenario
{
    /// <summary>What one scenario step does.</summary>
    public enum NexScenarioStepKind
    {
        /// <summary>Resolve an automation id and make it the current target.</summary>
        Find = 0,

        /// <summary>Click the current target.</summary>
        Click = 1,

        /// <summary>Write a value into the state store.</summary>
        SetState = 2,

        /// <summary>Poll a state value until it satisfies a comparison, or give up.</summary>
        WaitUntil = 3,

        /// <summary>Wait a fixed amount of time, measured on the scenario's time source.</summary>
        WaitForSeconds = 9,

        AssertVisible = 4,
        AssertHidden = 5,
        AssertText = 6,
        AssertState = 7,

        /// <summary>Fail if the screen raised any error while the scenario ran.</summary>
        AssertNoErrors = 8
    }

    /// <summary>
    /// One step of a scenario. A flat record rather than a class hierarchy so a scenario is plain
    /// data that can be written in code today and deserialised from a recording later.
    /// </summary>
    public struct NexScenarioStep
    {
        public NexScenarioStepKind Kind;

        /// <summary>Automation id for <see cref="NexScenarioStepKind.Find"/>.</summary>
        public string AutomationId;

        /// <summary>State key for the state-related kinds.</summary>
        public string Key;

        /// <summary>Expected or assigned value. Compared numerically when both sides parse as numbers.</summary>
        public string Value;

        public NexComparison Comparison;

        /// <summary>
        /// How many polls a <see cref="NexScenarioStepKind.WaitUntil"/> gets before it fails.
        /// Counted in runner steps, not seconds - see <see cref="NexScenarioRunner"/>.
        /// </summary>
        public int PollBudget;

        /// <summary>Seconds for <see cref="NexScenarioStepKind.WaitForSeconds"/>.</summary>
        public double Seconds;

        public override string ToString()
        {
            switch (Kind)
            {
                case NexScenarioStepKind.Find: return "Find(" + AutomationId + ")";
                case NexScenarioStepKind.Click: return "Click";
                case NexScenarioStepKind.SetState: return "SetState(" + Key + " = " + Value + ")";
                case NexScenarioStepKind.WaitUntil: return "WaitUntil(" + Key + " " + Comparison + " " + Value + ")";
                case NexScenarioStepKind.WaitForSeconds: return "WaitForSeconds(" + Seconds + ")";
                case NexScenarioStepKind.AssertText: return "AssertText(" + Value + ")";
                case NexScenarioStepKind.AssertState: return "AssertState(" + Key + " " + Comparison + " " + Value + ")";
                default: return Kind.ToString();
            }
        }
    }

    /// <summary>
    /// A named sequence of steps describing one user journey through a screen.
    /// </summary>
    /// <remarks>
    /// Written with the fluent builder below rather than assembled by hand, so a scenario reads
    /// like the thing it describes:
    ///
    /// <code>
    /// NexScenario.Named("PurchaseItem")
    ///     .Find("store.item.purchase")
    ///     .Click()
    ///     .WaitUntil("Store.IsPurchasing", NexComparison.Equals, "false")
    ///     .AssertState("Player.Currency", NexComparison.Equals, "500")
    ///     .AssertNoErrors();
    /// </code>
    ///
    /// Immutable once built and free of any Unity type, so the same scenario runs in an edit-mode
    /// test, a play-mode test and eventually against a player build.
    /// </remarks>
    public sealed class NexScenario
    {
        /// <summary>Polls a WaitUntil gets when the author does not say. Generous enough for a load, short enough to fail fast.</summary>
        public const int DefaultPollBudget = 120;

        private readonly List<NexScenarioStep> _steps = new List<NexScenarioStep>();

        public string Name { get; }

        public IReadOnlyList<NexScenarioStep> Steps => _steps;

        private NexScenario(string name) => Name = name ?? "Scenario";

        public static NexScenario Named(string name) => new NexScenario(name);

        // ---- actions --------------------------------------------------------

        public NexScenario Find(string automationId) => Add(new NexScenarioStep
        {
            Kind = NexScenarioStepKind.Find,
            AutomationId = automationId
        });

        public NexScenario Click() => Add(new NexScenarioStep { Kind = NexScenarioStepKind.Click });

        public NexScenario SetState(string key, string value) => Add(new NexScenarioStep
        {
            Kind = NexScenarioStepKind.SetState,
            Key = key,
            Value = value
        });

        public NexScenario WaitUntil(string key, NexComparison comparison, string value,
            int pollBudget = DefaultPollBudget) => Add(new NexScenarioStep
        {
            Kind = NexScenarioStepKind.WaitUntil,
            Key = key,
            Comparison = comparison,
            Value = value,
            PollBudget = pollBudget > 0 ? pollBudget : DefaultPollBudget
        });

        /// <summary>
        /// Waits a fixed amount of time on the runner's time source.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="WaitUntil"/> where a condition exists. A fixed wait passes or fails
        /// on how fast the machine is, which is how a suite ends up with tests that only fail on
        /// CI. This is here for the cases with nothing to observe - a purely visual settle.
        /// </remarks>
        public NexScenario WaitForSeconds(double seconds) => Add(new NexScenarioStep
        {
            Kind = NexScenarioStepKind.WaitForSeconds,
            Seconds = seconds > 0d ? seconds : 0d
        });

        // ---- assertions -----------------------------------------------------

        public NexScenario AssertVisible() => Add(new NexScenarioStep { Kind = NexScenarioStepKind.AssertVisible });

        public NexScenario AssertHidden() => Add(new NexScenarioStep { Kind = NexScenarioStepKind.AssertHidden });

        public NexScenario AssertText(string expected) => Add(new NexScenarioStep
        {
            Kind = NexScenarioStepKind.AssertText,
            Value = expected
        });

        public NexScenario AssertState(string key, NexComparison comparison, string value) => Add(new NexScenarioStep
        {
            Kind = NexScenarioStepKind.AssertState,
            Key = key,
            Comparison = comparison,
            Value = value
        });

        public NexScenario AssertNoErrors() => Add(new NexScenarioStep { Kind = NexScenarioStepKind.AssertNoErrors });

        private NexScenario Add(NexScenarioStep step)
        {
            _steps.Add(step);
            return this;
        }
    }
}
