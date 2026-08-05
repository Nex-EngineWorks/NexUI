using System.Collections.Generic;

namespace emiteat.NexUI.Diagnostics
{
    /// <summary>
    /// The error code catalog. Every diagnostic NexUI raises has an entry here, and the
    /// <c>ErrorCodeCatalog.md</c> document is generated from <see cref="All"/> rather than
    /// maintained by hand - a code with no entry is a bug, not an undocumented feature.
    /// </summary>
    /// <remarks>
    /// Codes are <c>NEX-{SUBSYSTEM}-{NUMBER}</c> and are permanent. Retiring a check means
    /// marking its entry obsolete, never reusing the number for something else, because codes
    /// end up in user bug reports and in saved build reports.
    ///
    /// Subsystem ranges: DOC 1xxx (document/authoring), SER 2xxx (serialization),
    /// CMP 3xxx (compiler), BND 4xxx (binding), LAY 5xxx (layout), RT 6xxx (runtime),
    /// BLD 8xxx (build/publish).
    /// </remarks>
    public static class NexDiagnosticCodes
    {
        // ---- DOC: authoring document ---------------------------------------
        public const string ScreenIdMissing = "NEX-DOC-1001";
        public const string ElementIdMissing = "NEX-DOC-1002";
        public const string DuplicateElementId = "NEX-DOC-1003";
        public const string ParentNotFound = "NEX-DOC-1004";
        public const string ParentCycle = "NEX-DOC-1005";
        public const string EmptyScreen = "NEX-DOC-1006";
        public const string DuplicateAutomationId = "NEX-DOC-1007";

        // ---- CMP: compiler --------------------------------------------------
        public const string CompileFailed = "NEX-CMP-3001";
        public const string UnknownElementType = "NEX-CMP-3002";
        public const string BackendUnsupportedNode = "NEX-CMP-3003";
        public const string NoDocument = "NEX-CMP-3004";

        // ---- BND: binding ---------------------------------------------------
        public const string CommandOnNonClickableNode = "NEX-BND-4001";
        public const string TextBindingOnNonTextNode = "NEX-BND-4002";
        public const string InteractionTargetNotFound = "NEX-BND-4003";
        public const string TriggerNotRaisableByNode = "NEX-BND-4004";
        public const string InteractionHasNoActions = "NEX-BND-4005";
        public const string InteractionActionIncomplete = "NEX-BND-4006";
        public const string InteractionPhaseUnreachable = "NEX-BND-4007";

        // ---- RT: runtime ----------------------------------------------------
        public const string NoCommandHandler = "NEX-RT-6001";
        public const string CommandHandlerThrew = "NEX-RT-6002";
        public const string ProgramSchemaMismatch = "NEX-RT-6003";
        public const string InteractionPortMissing = "NEX-RT-6004";

        // ---- ACC: accessibility ---------------------------------------------
        public const string InteractiveNodeHasNoAccessibleName = "NEX-ACC-7001";
        public const string ImageRoleWithoutLabel = "NEX-ACC-7002";

        // ---- BLD: build / publish -------------------------------------------
        public const string PublishFailed = "NEX-BLD-8001";
        public const string PublishPathInvalid = "NEX-BLD-8002";

        // ---- TEST: scenario replay ------------------------------------------
        public const string ScenarioElementNotFound = "NEX-TEST-9001";
        public const string ScenarioNoTarget = "NEX-TEST-9002";
        public const string ScenarioAssertionFailed = "NEX-TEST-9003";
        public const string ScenarioTimedOut = "NEX-TEST-9004";
        public const string ScenarioReportedDiagnostics = "NEX-TEST-9005";

        /// <summary>Static description of one code, used for reports and generated docs.</summary>
        public sealed class Entry
        {
            public string Code { get; }
            public NexSeverity DefaultSeverity { get; }
            public string Summary { get; }
            public string Resolution { get; }

            public Entry(string code, NexSeverity defaultSeverity, string summary, string resolution)
            {
                Code = code;
                DefaultSeverity = defaultSeverity;
                Summary = summary;
                Resolution = resolution;
            }
        }

        private static readonly Entry[] _all =
        {
            new Entry(ScreenIdMissing, NexSeverity.Error,
                "The screen has no screen id.",
                "Set a screen id on the metadata asset. It is the key the runtime opens the screen by."),

            new Entry(ElementIdMissing, NexSeverity.Error,
                "An element has no element id.",
                "Give the element a name in the hierarchy panel; bindings and focus links reference it by id."),

            new Entry(DuplicateElementId, NexSeverity.Error,
                "Two elements on the same screen share an element id.",
                "Rename one of them. Duplicate ids make bindings ambiguous at runtime."),

            new Entry(ParentNotFound, NexSeverity.Error,
                "An element points at a parent that is not on this screen.",
                "Re-parent the element, or delete it if it is orphaned."),

            new Entry(ParentCycle, NexSeverity.Error,
                "Element parenting forms a cycle.",
                "Break the cycle in the hierarchy panel; a screen must be a tree."),

            new Entry(EmptyScreen, NexSeverity.Warning,
                "The screen compiled with no elements.",
                "Add at least one element, or delete the screen if it is no longer used."),

            new Entry(DuplicateAutomationId, NexSeverity.Error,
                "Two elements on the same screen share an automation id.",
                "Rename one of them. A test that looks the id up would silently get whichever element compiled first."),

            new Entry(InteractiveNodeHasNoAccessibleName, NexSeverity.Warning,
                "An interactive node announces nothing to assistive technology.",
                "Give the element an accessibility label, or visible text. An icon-only button is "
                + "unreachable by a screen reader without one, and the EU Accessibility Act treats "
                + "that as a defect for products sold in the EU."),

            new Entry(ImageRoleWithoutLabel, NexSeverity.Suggestion,
                "An element marked as a meaningful image has no accessibility label.",
                "Describe what the image conveys, or set its role to None so it is announced as "
                + "decoration and skipped rather than read out as an unnamed image."),

            new Entry(CompileFailed, NexSeverity.Error,
                "Screen compilation failed.",
                "Follow the cause chain to the root diagnostic; nothing was published."),

            new Entry(UnknownElementType, NexSeverity.Error,
                "The element's type is not registered in the component registry.",
                "Re-create the element from the components panel, or register the missing component type."),

            new Entry(BackendUnsupportedNode, NexSeverity.Warning,
                "The element's type has no representation on the target backend.",
                "Switch backend, or replace the element with one the backend supports. The node is compiled as a plain panel."),

            new Entry(NoDocument, NexSeverity.Error,
                "No metadata asset was supplied to the compiler.",
                "Open a screen in NexUI Studio before compiling."),

            new Entry(CommandOnNonClickableNode, NexSeverity.Warning,
                "A command binding sits on an element that cannot be clicked.",
                "Move the command binding to a Button, or remove it. It will never fire where it is."),

            new Entry(TextBindingOnNonTextNode, NexSeverity.Warning,
                "A text binding sits on an element that displays no text.",
                "Move the binding to a Label or a Button, or remove it."),

            new Entry(InteractionTargetNotFound, NexSeverity.Error,
                "An interaction action targets an element that is not on this screen.",
                "Pick an existing element as the target, or delete the action. The rule cannot run as authored."),

            new Entry(TriggerNotRaisableByNode, NexSeverity.Warning,
                "An interaction uses a trigger this element can never raise.",
                "OnClick needs a clickable element. Move the rule to a Button, or change the trigger."),

            new Entry(InteractionHasNoActions, NexSeverity.Warning,
                "An interaction rule has no actions.",
                "Add an action, or delete the rule. As authored it evaluates its condition and does nothing."),

            new Entry(InteractionActionIncomplete, NexSeverity.Error,
                "An interaction action is missing a value it needs to run.",
                "Fill in the command id, state key or target the action requires."),

            new Entry(NoCommandHandler, NexSeverity.Warning,
                "A command fired at runtime with no registered handler.",
                "Register a handler with NexCommandRouter.Register(commandId, handler) during bootstrap."),

            new Entry(CommandHandlerThrew, NexSeverity.Error,
                "A command handler threw.",
                "See the inner exception in the detail field; the interaction was aborted at that step."),

            new Entry(InteractionPhaseUnreachable, NexSeverity.Warning,
                "An interaction rule listens on a phase that can never deliver it.",
                "Capture and Bubble need a descendant to raise the event, and only click-like triggers travel at all."),

            new Entry(InteractionPortMissing, NexSeverity.Error,
                "An interaction action needs a runtime service that was not supplied.",
                "Pass the state store / screen surface when building the screen; the rest of the rule still ran."),

            new Entry(ProgramSchemaMismatch, NexSeverity.Error,
                "The compiled screen was produced by a different compiler version.",
                "Recompile the screen from NexUI Studio."),

            new Entry(PublishFailed, NexSeverity.Error,
                "The compiled screen could not be written to disk.",
                "The previously published asset was left untouched. Check the output folder is writable."),

            new Entry(PublishPathInvalid, NexSeverity.Error,
                "The publish path is empty or outside the project.",
                "Set an output path under Assets/ in the NexUI settings."),

            new Entry(ScenarioElementNotFound, NexSeverity.Error,
                "A scenario looked for an automation id the screen does not have.",
                "Check the id on the element, or the scenario is pointed at the wrong screen."),

            new Entry(ScenarioNoTarget, NexSeverity.Error,
                "A scenario step acted before anything was found.",
                "Put a Find step before the step that acts on the element."),

            new Entry(ScenarioAssertionFailed, NexSeverity.Error,
                "A scenario assertion did not hold.",
                "The detail names what was expected and what was actually there."),

            new Entry(ScenarioTimedOut, NexSeverity.Error,
                "A scenario waited for a condition that never became true.",
                "Check the condition, or raise the step's poll budget if the work is genuinely slower."),

            new Entry(ScenarioReportedDiagnostics, NexSeverity.Error,
                "The screen raised diagnostics while the scenario ran.",
                "The scenario asked for none; read the collected diagnostics for what happened.")
        };

        /// <summary>Every catalogued code, in declaration order.</summary>
        public static IReadOnlyList<Entry> All => _all;

        /// <summary>
        /// The subsystem segment of a code - <c>BND</c> for <c>NEX-BND-4001</c>.
        /// </summary>
        /// <remarks>
        /// Parsed rather than stored because the code already carries it, and a second field
        /// saying the same thing is a second thing that can disagree.
        /// </remarks>
        public static string SubsystemOf(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;

            var first = code.IndexOf('-');
            if (first < 0) return string.Empty;

            var second = code.IndexOf('-', first + 1);
            return second < 0 ? string.Empty : code.Substring(first + 1, second - first - 1);
        }

        /// <summary>Every subsystem the catalog uses, for a filter dropdown.</summary>
        public static IEnumerable<string> Subsystems()
        {
            var seen = new List<string>();
            for (int i = 0; i < _all.Length; i++)
            {
                var subsystem = SubsystemOf(_all[i].Code);
                if (!string.IsNullOrEmpty(subsystem) && !seen.Contains(subsystem)) seen.Add(subsystem);
            }
            return seen;
        }

        public static Entry Find(string code)
        {
            for (int i = 0; i < _all.Length; i++)
                if (_all[i].Code == code) return _all[i];
            return null;
        }

        /// <summary>
        /// Builds a diagnostic from the catalog so the default severity and the resolution text
        /// stay in one place. <paramref name="message"/> overrides the catalog summary when the
        /// call site can say something more specific.
        /// </summary>
        public static NexDiagnostic Create(string code, NexSourceLocation location = default,
            string message = null, string detail = null, NexSeverity? severity = null,
            NexDiagnostic cause = null)
        {
            var entry = Find(code);
            return new NexDiagnostic(
                code,
                severity ?? (entry?.DefaultSeverity ?? NexSeverity.Error),
                message ?? entry?.Summary ?? code,
                location,
                detail,
                entry?.Resolution,
                cause);
        }
    }
}
