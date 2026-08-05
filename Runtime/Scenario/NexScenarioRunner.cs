using System.Collections.Generic;
using System.Text;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;

namespace emiteat.NexUI.Scenario
{
    /// <summary>How one step ended.</summary>
    public enum NexScenarioStepStatus
    {
        Passed = 0,
        Failed = 1,

        /// <summary>Never reached, because an earlier step failed.</summary>
        NotRun = 2
    }

    public struct NexScenarioStepResult
    {
        public NexScenarioStep Step;
        public NexScenarioStepStatus Status;

        /// <summary>What was seen, for a failure. Empty when the step passed.</summary>
        public string Detail;

        /// <summary>Polls a WaitUntil actually used, so a flaky wait is visible before it fails.</summary>
        public int Polls;
    }

    /// <summary>What a scenario run produced.</summary>
    public sealed class NexScenarioResult
    {
        public string Name { get; }
        public List<NexScenarioStepResult> Steps { get; } = new List<NexScenarioStepResult>();

        /// <summary>The first failure, or null.</summary>
        public NexDiagnostic Failure { get; internal set; }

        public bool Succeeded => Failure == null;

        public NexScenarioResult(string name) => Name = name ?? "Scenario";

        /// <summary>
        /// Text report in the same shape as an interaction flow trace, so both read the same way
        /// in a console, a CI log or a bug report.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Succeeded ? "✓ " : "✗ ").Append("Scenario ").Append(Name).Append('\n');

            for (int i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];
                sb.Append("  ");

                switch (step.Status)
                {
                    case NexScenarioStepStatus.Passed: sb.Append("✓ "); break;
                    case NexScenarioStepStatus.Failed: sb.Append("✗ "); break;
                    default: sb.Append("- "); break;
                }

                sb.Append(step.Step);

                if (step.Polls > 1) sb.Append("  (").Append(step.Polls).Append(" polls)");
                if (!string.IsNullOrEmpty(step.Detail)) sb.Append("  ").Append(step.Detail);
                sb.Append('\n');
            }

            if (Failure != null) sb.Append('\n').Append(Failure.ToDetailedString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Executes a scenario against a running screen, one step per <see cref="MoveNext"/>.
    /// </summary>
    /// <remarks>
    /// Caller-driven rather than self-driving, and this is the important design decision. A
    /// scenario has to wait for things - a command to finish, a screen to open - and the obvious
    /// way to express that is a timer. But NexUI has no time abstraction yet (the feature
    /// specification's Time Source is not built), and a runner that reached for
    /// <c>Time.deltaTime</c> would be wrong under pause, wrong under a timeline scrub and
    /// impossible to run deterministically twice.
    ///
    /// So the runner counts <em>polls</em>, and the caller decides what a poll is worth:
    ///
    /// <code>
    /// var runner = new NexScenarioRunner(scenario, world);
    /// while (runner.MoveNext()) yield return null;   // a poll is a frame
    /// Assert.IsTrue(runner.Result.Succeeded, runner.Result.ToString());
    /// </code>
    ///
    /// An edit-mode test drives the same loop with no yield at all. Nothing about the runner
    /// changes, and both runs are reproducible.
    ///
    /// Failure stops the run. Later steps are reported as <see cref="NexScenarioStepStatus.NotRun"/>
    /// rather than attempted, because a scenario is a sequence: once "click purchase" failed,
    /// "assert the receipt appeared" is not an independent second failure worth reporting.
    /// </remarks>
    public sealed class NexScenarioRunner
    {
        private readonly NexScenario _scenario;
        private readonly INexScenarioWorld _world;
        private readonly Time.INexTimeSource _time;

        private int _stepIndex;
        private int _polls;
        private int _target = -1;
        private bool _done;
        private double _deadline;
        private bool _waiting;

        public NexScenarioResult Result { get; }

        public bool IsDone => _done;

        /// <param name="time">
        /// Clock for <c>WaitForSeconds</c>. Pass a <c>NexManualTime</c> to make a run reproducible;
        /// omit it to use the shared default.
        /// </param>
        public NexScenarioRunner(NexScenario scenario, INexScenarioWorld world,
            Time.INexTimeSource time = null)
        {
            _scenario = scenario;
            _world = world;
            _time = time ?? Time.NexTime.Default;
            Result = new NexScenarioResult(scenario != null ? scenario.Name : null);
        }

        /// <summary>
        /// Advances the run. Returns true while there is more to do, false once the scenario has
        /// passed or failed.
        /// </summary>
        public bool MoveNext()
        {
            if (_done) return false;

            if (_scenario == null || _world == null)
            {
                Fail(default, NexDiagnosticCodes.ScenarioNoTarget, "No scenario or no world was supplied.");
                return false;
            }

            if (_stepIndex >= _scenario.Steps.Count)
            {
                _done = true;
                return false;
            }

            var step = _scenario.Steps[_stepIndex];

            // The waiting steps can occupy more than one poll; everything else completes or fails
            // on the poll it starts.
            if (step.Kind == NexScenarioStepKind.WaitUntil) return PollWait(step);
            if (step.Kind == NexScenarioStepKind.WaitForSeconds) return PollSleep(step);

            Execute(step);
            if (_done) return false;

            _stepIndex++;
            return _stepIndex < _scenario.Steps.Count;
        }

        /// <summary>Drives the whole scenario without yielding. For edit-mode tests and headless runs.</summary>
        public NexScenarioResult RunToCompletion()
        {
            while (MoveNext()) { }
            return Result;
        }

        // ---- steps ----------------------------------------------------------

        private bool PollWait(NexScenarioStep step)
        {
            _polls++;

            if (_world.TryGetState(step.Key, out var value) &&
                NexValueComparison.Matches(value, step.Comparison, step.Value))
            {
                Pass(step, _polls);
                _polls = 0;
                _stepIndex++;
                return _stepIndex < _scenario.Steps.Count;
            }

            if (_polls < step.PollBudget) return true;

            var seen = _world.TryGetState(step.Key, out var last) ? NexValueComparison.Describe(last) : "<unset>";
            Fail(step, NexDiagnosticCodes.ScenarioTimedOut,
                "Waited " + step.PollBudget + " polls for '" + step.Key + " " + step.Comparison + " " +
                step.Value + "'; last saw '" + seen + "'.", _polls);
            return false;
        }

        /// <summary>
        /// Waits until the time source has moved past a deadline.
        /// </summary>
        /// <remarks>
        /// The deadline is captured once, on the first poll, rather than accumulating elapsed time
        /// per poll. Accumulating drifts with the poll rate and, worse, behaves nonsensically if
        /// the clock is scrubbed backwards - which <c>NexManualTime.SeekTo</c> explicitly allows.
        /// </remarks>
        private bool PollSleep(NexScenarioStep step)
        {
            _polls++;

            if (!_waiting)
            {
                _waiting = true;
                _deadline = _time.Now + step.Seconds;
            }

            if (_time.Now < _deadline) return true;

            _waiting = false;
            Pass(step, _polls);
            _polls = 0;
            _stepIndex++;
            return _stepIndex < _scenario.Steps.Count;
        }

        private void Execute(NexScenarioStep step)
        {
            switch (step.Kind)
            {
                case NexScenarioStepKind.Find:
                {
                    if (!_world.TryFind(step.AutomationId, out var index))
                    {
                        Fail(step, NexDiagnosticCodes.ScenarioElementNotFound,
                            "No element on this screen has automation id '" + step.AutomationId + "'.");
                        return;
                    }

                    _target = index;
                    Pass(step);
                    return;
                }

                case NexScenarioStepKind.Click:
                {
                    if (!RequireTarget(step)) return;
                    _world.Click(_target);
                    Pass(step);
                    return;
                }

                case NexScenarioStepKind.SetState:
                {
                    _world.SetState(step.Key, step.Value);
                    Pass(step);
                    return;
                }

                case NexScenarioStepKind.AssertVisible:
                case NexScenarioStepKind.AssertHidden:
                {
                    if (!RequireTarget(step)) return;

                    var visible = _world.IsVisible(_target);
                    var wanted = step.Kind == NexScenarioStepKind.AssertVisible;

                    if (visible == wanted) Pass(step);
                    else Fail(step, NexDiagnosticCodes.ScenarioAssertionFailed,
                        "Expected the element to be " + (wanted ? "visible" : "hidden") + ", but it was not.");
                    return;
                }

                case NexScenarioStepKind.AssertText:
                {
                    if (!RequireTarget(step)) return;

                    var text = _world.GetText(_target) ?? string.Empty;
                    if (string.Equals(text, step.Value ?? string.Empty, System.StringComparison.Ordinal)) Pass(step);
                    else Fail(step, NexDiagnosticCodes.ScenarioAssertionFailed,
                        "Expected text '" + step.Value + "', found '" + text + "'.");
                    return;
                }

                case NexScenarioStepKind.AssertState:
                {
                    var present = _world.TryGetState(step.Key, out var value);
                    if (present && NexValueComparison.Matches(value, step.Comparison, step.Value))
                    {
                        Pass(step);
                        return;
                    }

                    Fail(step, NexDiagnosticCodes.ScenarioAssertionFailed,
                        "Expected '" + step.Key + " " + step.Comparison + " " + step.Value + "', found '" +
                        (present ? NexValueComparison.Describe(value) : "<unset>") + "'.");
                    return;
                }

                case NexScenarioStepKind.AssertNoErrors:
                {
                    var errors = CountErrors();
                    if (errors == 0) Pass(step);
                    else Fail(step, NexDiagnosticCodes.ScenarioReportedDiagnostics,
                        "The screen raised " + errors + " error(s) while the scenario ran.");
                    return;
                }
            }
        }

        private bool RequireTarget(NexScenarioStep step)
        {
            if (_target >= 0) return true;

            Fail(step, NexDiagnosticCodes.ScenarioNoTarget,
                "This step acts on an element, but no Find step has run yet.");
            return false;
        }

        private int CountErrors()
        {
            var diagnostics = _world.Diagnostics;
            if (diagnostics == null) return 0;

            var count = 0;
            for (int i = 0; i < diagnostics.Count; i++)
                if (diagnostics[i] != null && diagnostics[i].Severity >= NexSeverity.Error) count++;
            return count;
        }

        // ---- reporting ------------------------------------------------------

        private void Pass(NexScenarioStep step, int polls = 0)
            => Result.Steps.Add(new NexScenarioStepResult
            {
                Step = step,
                Status = NexScenarioStepStatus.Passed,
                Polls = polls
            });

        private void Fail(NexScenarioStep step, string code, string detail, int polls = 0)
        {
            Result.Steps.Add(new NexScenarioStepResult
            {
                Step = step,
                Status = NexScenarioStepStatus.Failed,
                Detail = detail,
                Polls = polls
            });

            Result.Failure = NexDiagnosticCodes.Create(code, default,
                "Scenario '" + Result.Name + "' failed at step " + (_stepIndex + 1) + ": " + step + ".", detail);

            // Everything after the failure is reported as not run rather than attempted.
            for (int i = _stepIndex + 1; _scenario != null && i < _scenario.Steps.Count; i++)
                Result.Steps.Add(new NexScenarioStepResult
                {
                    Step = _scenario.Steps[i],
                    Status = NexScenarioStepStatus.NotRun
                });

            _done = true;
        }
    }
}
