using System;
using System.Collections.Generic;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Flow;

namespace emiteat.NexUI.Interaction
{
    /// <summary>
    /// Runs the compiled interaction rules for one screen instance.
    /// </summary>
    /// <remarks>
    /// The entire engine is this one class because the compiler already did the hard parts:
    /// element ids are resolved to node indices, values are pre-parsed, rules are ordered, and
    /// anything that could not be resolved failed the compile. What is left at runtime is a scan,
    /// a comparison and a switch - which is why a click costs no allocation and no lookup.
    ///
    /// Nothing here throws. A rule that cannot run reports a diagnostic and the remaining rules
    /// still run, because one bad action must not take the rest of the screen's behaviour with it.
    /// </remarks>
    public sealed class NexInteractionRuntime
    {
        private readonly NexScreenProgram _program;
        private readonly NexInteractionProgram _interactions;
        private readonly NexCommandRouter _router;
        private readonly INexStateAccess _state;
        private readonly INexScreenSurface _surface;
        private readonly Time.INexTimeSource _time;
        private readonly Overrides.NexOverrideLedger _overrides;
        private readonly List<Continuation> _pending = new List<Continuation>();

        /// <summary>Raised for every diagnostic produced while running rules.</summary>
        public event Action<NexDiagnostic> DiagnosticRaised;

        /// <param name="time">
        /// Clock delays are measured on. Omit for the shared default; pass a <c>NexManualTime</c>
        /// to make a test deterministic.
        /// </param>
        /// <param name="overrides">
        /// Optional. When supplied, every property this engine changes is recorded so the debugger
        /// can say which rule did it.
        /// </param>
        public NexInteractionRuntime(NexScreenProgram program, NexCommandRouter router,
            INexStateAccess state, INexScreenSurface surface, Time.INexTimeSource time = null,
            Overrides.NexOverrideLedger overrides = null)
        {
            _program = program;
            _interactions = program != null ? program.Interactions : null;
            _router = router;
            _state = state;
            _surface = surface;
            _time = time ?? Time.NexTime.Default;
            _overrides = overrides;
        }

        /// <summary>True when this screen authored no rules at all - lets a backend skip wiring triggers.</summary>
        public bool IsEmpty => _interactions == null || _interactions.IsEmpty;

        public bool HasAnyTrigger(NexTrigger trigger)
            => _interactions != null && _interactions.HasAnyTrigger(trigger);

        /// <summary>
        /// Node indices this screen wires a click listener onto.
        /// </summary>
        /// <remarks>
        /// With bubbling, the nodes that need a listener are no longer just the ones that own a
        /// rule: a list whose items bubble needs the <em>items</em> listening, not the list. The
        /// backend asks for this rather than working it out, so the rule lives in one place.
        /// </remarks>
        public bool WantsClickListener(int nodeIndex) => WantsListener(nodeIndex, NexTrigger.OnClick);

        /// <summary>
        /// Whether this node must report <paramref name="trigger"/> to the engine.
        /// </summary>
        /// <remarks>
        /// The generalisation of <see cref="WantsClickListener"/>, kept because the answer is the
        /// same shape for every trigger: a node listens either because it owns a rule, or because
        /// some ancestor's rule propagates and this node is where the event originates.
        ///
        /// Asked per trigger rather than per node so a screen that authored only a hover pays for
        /// no press listeners - the "pay for what you use" rule applied one event at a time.
        /// </remarks>
        public bool WantsListener(int nodeIndex, NexTrigger trigger)
        {
            if (IsEmpty) return false;

            // Any propagating rule means every node has to report the event, because the rule that
            // cares sits somewhere above it.
            if (_interactions.HasPropagatingRules(trigger)) return true;

            foreach (var _ in _interactions.RulesFor(nodeIndex, trigger, NexPhase.Target))
                return true;

            return false;
        }

        /// <summary>
        /// Raises a trigger on one node and runs whatever it authored.
        /// </summary>
        /// <remarks>
        /// The whole evaluation happens inside a single flow scope, so a trace shows the trigger,
        /// the condition verdict and each action as one chain rather than as unrelated log lines.
        /// A skipped rule is recorded (at Full level) rather than dropped, because "the condition
        /// was false" is the answer to the most common interaction bug there is.
        /// </remarks>
        /// <summary>
        /// State key holding the element currently being dragged, readable by a drop rule.
        /// </summary>
        /// <remarks>
        /// A drop is the one trigger whose subject is not the element that started the gesture, so
        /// the rule needs a second piece of information to be useful: a slot that accepts weapons
        /// but not potions cannot decide anything from "something was dropped on me".
        ///
        /// Published as ordinary state rather than as a new argument on <see cref="Fire"/> because
        /// the authoring model already knows how to read state - a drop rule filters on this with
        /// the same condition field every other rule uses, and needs no new concept.
        /// </remarks>
        public const string DragSourceKey = "nexui.drag.source";

        /// <summary>Publishes the element a drag started from. Call when the drag begins.</summary>
        public void SetDragSource(int nodeIndex)
        {
            if (_state == null) return;
            _state.Set(DragSourceKey, IdentityOf(nodeIndex));
        }

        /// <summary>
        /// Clears the drag source. Call when the drag ends.
        /// </summary>
        /// <remarks>
        /// Safe to call after a drop: uGUI delivers the drop before the drag ends, so the value is
        /// still readable while the drop rule runs and is gone before the next gesture starts.
        /// Leaving it set would let a later, unrelated rule read a stale source as if it were live.
        /// </remarks>
        public void ClearDragSource() => _state?.Set(DragSourceKey, string.Empty);

        /// <summary>
        /// How a node names itself to an authored rule.
        /// </summary>
        /// <remarks>
        /// The automation id first, because it is the handle an author deliberately assigned and
        /// which survives renaming and restructuring. The authoring path is the fallback: readable,
        /// but it changes when the screen is reorganised.
        /// </remarks>
        private string IdentityOf(int nodeIndex)
        {
            if (_program == null || nodeIndex < 0 || nodeIndex >= _program.Nodes.Length) return string.Empty;

            var automationId = _program.Nodes[nodeIndex].AutomationId;
            return !string.IsNullOrEmpty(automationId) ? automationId : PathOf(nodeIndex);
        }

        public void Fire(int nodeIndex, NexTrigger trigger)
        {
            if (IsEmpty) return;

            var originPath = PathOf(nodeIndex);

            using (var scope = NexFlowTrace.Begin(ScreenId + "/" + originPath))
            {
                scope.Step(originPath, "Trigger." + trigger);

                // Lifecycle triggers belong to the node that raised them, not to a pointer travelling
                // through a hierarchy, so they never propagate. Bubbling OnShow would fire an
                // ancestor's rule once per descendant that appeared, which is nobody's intent.
                if (!Propagates(trigger) || !_interactions.HasPropagatingRules(trigger))
                {
                    RunRulesAt(nodeIndex, trigger, NexPhase.Target, scope, originPath);
                    return;
                }

                var ancestors = AncestorsOf(nodeIndex);

                // Capture runs outermost-first, so an ancestor can intercept before the target acts.
                for (int i = ancestors.Count - 1; i >= 0; i--)
                    if (RunRulesAt(ancestors[i], trigger, NexPhase.Capture, scope, originPath)) return;

                if (RunRulesAt(nodeIndex, trigger, NexPhase.Target, scope, originPath)) return;

                // Bubble runs innermost-first: the closest ancestor gets to claim the event before
                // an outer one does, which is what makes "handled" meaningful.
                for (int i = 0; i < ancestors.Count; i++)
                    if (RunRulesAt(ancestors[i], trigger, NexPhase.Bubble, scope, originPath)) return;
            }
        }

        /// <summary>Whether a trigger travels through the hierarchy at all.</summary>
        /// <summary>
        /// Whether a trigger travels through the element tree.
        /// </summary>
        /// <remarks>
        /// Everything raised by a node propagates; only the screen lifecycle does not. Show and
        /// hide already reach every node, so bubbling one would fire an ancestor's rule once per
        /// descendant that appeared - which is nobody's intent.
        ///
        /// Must agree with the compiler's rule of the same name: the compiler drops a rule it
        /// believes can never be reached, and if these two disagree the rule is either dropped
        /// while it would have worked or kept while it never fires.
        /// </remarks>
        private static bool Propagates(NexTrigger trigger)
            => trigger != NexTrigger.OnShow && trigger != NexTrigger.OnHide;

        /// <summary>
        /// Runs the rules a node listens with in one phase.
        /// Returns true when a rule stopped the event and the walk must end.
        /// </summary>
        private bool RunRulesAt(int nodeIndex, NexTrigger trigger, NexPhase phase,
            NexFlowScope scope, string originPath)
        {
            foreach (var rule in _interactions.RulesFor(nodeIndex, trigger, phase))
            {
                var rulePath = PathOf(nodeIndex);

                if (phase != NexPhase.Target)
                    scope.Step(rulePath, phase.ToString());

                if (rule.HasCondition && !EvaluateCondition(rule, scope, rulePath)) continue;

                RunActions(rule, scope, rulePath);

                if (rule.StopsPropagation)
                {
                    scope.Step(rulePath, "StopPropagation", NexFlowStatus.Ok, "origin " + originPath);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The node's ancestors, innermost first.
        /// </summary>
        /// <remarks>
        /// Guarded against a malformed parent chain even though the compiler rejects cycles: this
        /// runs on every click, and a hang here would take the game with it.
        /// </remarks>
        private List<int> AncestorsOf(int nodeIndex)
        {
            var result = new List<int>();
            if (_program == null) return result;

            var nodes = _program.Nodes;
            var current = nodeIndex;
            var guard = 0;

            while (guard++ < nodes.Length)
            {
                if (current < 0 || current >= nodes.Length) break;

                var parent = nodes[current].ParentIndex;
                if (parent < 0 || parent >= nodes.Length) break;

                result.Add(parent);
                current = parent;
            }

            return result;
        }

        /// <summary>
        /// Raises a screen-wide trigger such as <see cref="NexTrigger.OnShow"/> on every node that
        /// authored one, each node exactly once.
        /// </summary>
        /// <remarks>
        /// Walks the rule list rather than every node, so a screen with 300 nodes and one OnShow
        /// rule does one unit of work. The visited set is allocated only when rules actually exist
        /// for the trigger, and a node is fired once no matter how many rules it owns - firing per
        /// rule would run each of that node's rules once per rule.
        /// </remarks>
        public void FireAll(NexTrigger trigger)
        {
            if (IsEmpty || !_interactions.HasAnyTrigger(trigger)) return;

            var fired = new System.Collections.Generic.HashSet<int>();
            var rules = _interactions.Rules;

            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule.Trigger != trigger) continue;
                if (!fired.Add(rule.NodeIndex)) continue;

                Fire(rule.NodeIndex, trigger);
            }
        }

        // ---- condition ------------------------------------------------------

        /// <remarks>
        /// The scope is passed by value on purpose. <see cref="NexFlowScope"/> is a readonly
        /// struct whose per-step cursor lives on the record it points at, so a copy records into
        /// the same trace - and a <c>using</c> variable cannot be passed by <c>ref</c> anyway.
        /// </remarks>
        private bool EvaluateCondition(NexInteractionRule rule, NexFlowScope scope, string path)
        {
            object live = null;
            if (_state != null) _state.TryGet(rule.ConditionKey, out live);

            var pass = Compare(live, rule);

            if (pass)
                scope.Step("Condition", rule.ConditionKey + " " + rule.Comparison, NexFlowStatus.Ok,
                    Describe(live) + " vs " + rule.ConditionString);
            else
                scope.Skipped("Condition", rule.ConditionKey + " " + rule.Comparison,
                    Describe(live) + " vs " + rule.ConditionString + " → false");

            return pass;
        }

        /// <summary>
        /// Compares the live value against the authored one.
        /// </summary>
        /// <remarks>
        /// Delegates to <see cref="NexValueComparison"/> so an interaction condition and a scenario
        /// assertion answer the same question identically - otherwise a rule could fire in the game
        /// and fail its own test for reasons nobody could see.
        /// </remarks>
        private static bool Compare(object live, NexInteractionRule rule)
            => NexValueComparison.Matches(live, rule.Comparison, rule.ConditionString,
                rule.ConditionNumber, rule.ConditionIsNumeric);

        // ---- actions --------------------------------------------------------

        /// <summary>
        /// Runs a rule's actions from <paramref name="from"/>, stopping if one of them is a delay.
        /// </summary>
        /// <remarks>
        /// Returns the index the rule should resume at, or -1 when it finished. A rule is a
        /// sequence, so a delay simply parks the rest of it until <see cref="Tick"/> comes back.
        /// </remarks>
        private int RunActions(NexInteractionRule rule, NexFlowScope scope, string path, int from = 0)
        {
            for (int i = from; i < rule.ActionCount; i++)
            {
                var action = _interactions.ActionAt(rule.ActionStart + i);

                if (action.Kind == NexActionKind.Delay)
                {
                    Schedule(rule, path, i + 1, action.Seconds);
                    scope.Step(path, "Delay", NexFlowStatus.Ok, action.Seconds + "s");
                    return i + 1;
                }

                RunAction(action, rule, scope, path);
            }

            return -1;
        }

        /// <summary>
        /// Resumes rules whose delay has elapsed.
        /// </summary>
        /// <remarks>
        /// The backend pumps this once per frame, and only for screens that actually contain a
        /// delay (see <c>NexInteractionProgram.HasDelays</c>).
        ///
        /// Continuations are collected before any of them runs. A resumed action can fire a
        /// command that clicks something else and schedules more work, and mutating the list while
        /// walking it is how that turns into a missed or double-run action.
        /// </remarks>
        public void Tick()
        {
            if (_pending.Count == 0) return;

            var now = _time.Now;
            List<Continuation> due = null;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Deadline > now) continue;

                (due ??= new List<Continuation>()).Add(_pending[i]);
                _pending.RemoveAt(i);
            }

            if (due == null) return;

            // Removed back-to-front above, so restore authored order before running.
            due.Reverse();

            foreach (var continuation in due)
            {
                using (var scope = NexFlowTrace.Begin(ScreenId + "/" + continuation.Path))
                {
                    scope.Step(continuation.Path, "Resume", NexFlowStatus.Ok,
                        "after " + continuation.Rule.RuleId);
                    RunActions(continuation.Rule, scope, continuation.Path, continuation.NextAction);
                }
            }
        }

        /// <summary>
        /// Drops every parked continuation.
        /// </summary>
        /// <remarks>
        /// Called when the screen is torn down. Without it a delayed action would resume against a
        /// destroyed hierarchy - the single worst failure this feature can produce, because it
        /// happens after the user has already navigated away and looks like a bug in whatever
        /// screen they are on now.
        /// </remarks>
        public void CancelPending() => _pending.Clear();

        /// <summary>How many rules are parked mid-sequence. For the runtime debugger.</summary>
        public int PendingCount => _pending.Count;

        private void Schedule(NexInteractionRule rule, string path, int nextAction, double seconds)
            => _pending.Add(new Continuation
            {
                Rule = rule,
                Path = path,
                NextAction = nextAction,
                Deadline = _time.Now + (seconds > 0d ? seconds : 0d)
            });

        /// <summary>A rule parked partway through, waiting for its delay to elapse.</summary>
        private struct Continuation
        {
            public NexInteractionRule Rule;
            public string Path;
            public int NextAction;
            public double Deadline;
        }

        private void RunAction(NexInteractionAction action, NexInteractionRule rule,
            NexFlowScope scope, string path)
        {
            switch (action.Kind)
            {
                case NexActionKind.ExecuteCommand:
                {
                    if (_router == null)
                    {
                        scope.Failed("Command." + action.CommandId, "Dispatch",
                            NexDiagnosticCodes.NoCommandHandler, "No command router is wired to this screen.");
                        Raise(NexDiagnosticCodes.NoCommandHandler, rule, path,
                            "Command '" + action.CommandId + "' fired with no router attached.");
                        return;
                    }

                    scope.Step("Command." + action.CommandId, "Dispatch");

                    var result = _router.Dispatch(new NexCommandContext
                    {
                        CommandId = action.CommandId,
                        SenderPath = path,
                        SenderNodeId = NodeIdOf(rule.NodeIndex),
                        ScreenId = ScreenId
                    });

                    if (result.Handled) scope.Step("Handler", "Invoke");
                    else scope.Failed("Handler", "Invoke",
                        result.Diagnostic != null ? result.Diagnostic.Code : NexDiagnosticCodes.NoCommandHandler,
                        result.Diagnostic != null ? result.Diagnostic.Message : null);
                    return;
                }

                case NexActionKind.SetState:
                {
                    if (_state == null)
                    {
                        scope.Failed("State", "Set", NexDiagnosticCodes.InteractionPortMissing, action.StateKey);
                        Raise(NexDiagnosticCodes.InteractionPortMissing, rule, path,
                            "Rule sets state '" + action.StateKey + "' but no state store is attached.");
                        return;
                    }

                    object value = action.IsNumeric ? (object)action.NumberValue : action.StringValue;
                    _state.Set(action.StateKey, value);
                    scope.Step("State", "Set " + action.StateKey, NexFlowStatus.Ok, Describe(value));
                    return;
                }

                case NexActionKind.SetVisible:
                {
                    if (!RequireSurface(action, rule, scope, path)) return;
                    _surface.SetVisible(action.TargetNodeIndex, action.BoolValue);
                    _overrides?.Record(action.TargetNodeIndex, Overrides.NexOverrideProperty.Visible,
                        Overrides.NexOverrideSource.Interaction, action.BoolValue ? "true" : "false", rule.RuleId);
                    scope.Step(PathOf(action.TargetNodeIndex), "SetVisible", NexFlowStatus.Ok,
                        action.BoolValue ? "true" : "false");
                    return;
                }

                case NexActionKind.SetText:
                {
                    if (!RequireSurface(action, rule, scope, path)) return;
                    _surface.SetText(action.TargetNodeIndex, action.StringValue ?? string.Empty);
                    _overrides?.Record(action.TargetNodeIndex, Overrides.NexOverrideProperty.Text,
                        Overrides.NexOverrideSource.Interaction, action.StringValue, rule.RuleId);
                    scope.Step(PathOf(action.TargetNodeIndex), "SetText", NexFlowStatus.Ok, action.StringValue);
                    return;
                }
            }
        }

        private bool RequireSurface(NexInteractionAction action, NexInteractionRule rule,
            NexFlowScope scope, string path)
        {
            if (_surface != null && action.TargetNodeIndex >= 0) return true;

            scope.Failed("Surface", action.Kind.ToString(), NexDiagnosticCodes.InteractionPortMissing);
            Raise(NexDiagnosticCodes.InteractionPortMissing, rule, path,
                "Rule action '" + action.Kind + "' has no screen surface to act on.");
            return false;
        }

        // ---- helpers --------------------------------------------------------

        private string ScreenId => _program != null ? _program.ScreenId : string.Empty;

        private string PathOf(int nodeIndex)
        {
            if (_program == null || nodeIndex < 0 || nodeIndex >= _program.Nodes.Length) return "<unknown>";

            var path = _program.SourceMap.PathOfIndex(nodeIndex);
            return !string.IsNullOrEmpty(path) ? path : _program.Nodes[nodeIndex].Name;
        }

        private string NodeIdOf(int nodeIndex)
            => _program != null && nodeIndex >= 0 && nodeIndex < _program.Nodes.Length
                ? _program.Nodes[nodeIndex].NodeId
                : string.Empty;

        private static string Describe(object value)
            => value == null ? "null" : NexValueComparison.Describe(value);

        private void Raise(string code, NexInteractionRule rule, string path, string message)
        {
            var handler = DiagnosticRaised;
            if (handler == null) return;

            handler(NexDiagnosticCodes.Create(code,
                new NexSourceLocation(ScreenId, NodeIdOf(rule.NodeIndex), path, "interaction"),
                message, "Rule: " + rule.RuleId));
        }
    }
}
