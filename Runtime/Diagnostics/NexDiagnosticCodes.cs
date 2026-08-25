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
        public const string AppearanceNotCarried = "NEX-CMP-3005";
        public const string FragmentDefinitionMissing = "NEX-CMP-3006";
        public const string FragmentIncompatible = "NEX-CMP-3007";
        public const string FragmentShadowsAuthoredProperty = "NEX-CMP-3008";
        public const string BlockExpansionFailed = "NEX-CMP-3009";
        public const string BlockExpansionIncomplete = "NEX-CMP-3010";
        public const string BlockExpansionNote = "NEX-CMP-3011";
        public const string StateOverrideNotCarried = "NEX-CMP-3012";
        public const string ResponsiveOverrideNotCarried = "NEX-CMP-3013";
        public const string LocalizationKeyConflict = "NEX-CMP-3014";
        public const string PartOverrideNotCarried = "NEX-CMP-3015";

        // ---- BND: binding ---------------------------------------------------
        public const string CommandOnNonClickableNode = "NEX-BND-4001";
        public const string TextBindingOnNonTextNode = "NEX-BND-4002";
        public const string InteractionTargetNotFound = "NEX-BND-4003";
        public const string TriggerNotRaisableByNode = "NEX-BND-4004";
        public const string InteractionHasNoActions = "NEX-BND-4005";
        public const string InteractionActionIncomplete = "NEX-BND-4006";
        public const string InteractionPhaseUnreachable = "NEX-BND-4007";
        public const string TwoWayBindingOnReadOnlyNode = "NEX-BND-4008";
        public const string ValueBindingHasNoBackendTarget = "NEX-BND-4009";
        public const string ConverterKeyWithoutBinding = "NEX-BND-4010";

        // ---- RT: runtime ----------------------------------------------------
        public const string NoCommandHandler = "NEX-RT-6001";
        public const string CommandHandlerThrew = "NEX-RT-6002";
        public const string ProgramSchemaMismatch = "NEX-RT-6003";
        public const string InteractionPortMissing = "NEX-RT-6004";
        public const string LayoutFeatureUnsupported = "NEX-RT-6005";
        public const string AppearanceFeatureUnsupported = "NEX-RT-6006";
        public const string FeatureCarriedNotApplied = "NEX-RT-6007";
        public const string StateChannelUnsupported = "NEX-RT-6008";
        public const string PartNotBuilt = "NEX-RT-6009";

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

            new Entry(TwoWayBindingOnReadOnlyNode, NexSeverity.Warning,
                "A two-way binding was authored on a node that cannot write anything back.",
                "Set the binding to one-way, or use a control that accepts input. The binding still "
                + "reads; only the write-back half has nowhere to come from."),

            new Entry(ValueBindingHasNoBackendTarget, NexSeverity.Warning,
                "A value binding was authored on a node the backend builds with nothing to hold a value.",
                "The compiled node kinds are panel, image, label and button - none of them carries a "
                + "scalar. The binding is preserved in the program and starts working once the screen "
                + "uses a control that does."),

            new Entry(ConverterKeyWithoutBinding, NexSeverity.Suggestion,
                "A converter was named for a binding that is not set.",
                "Remove the converter key, or set the binding it was meant for. As it stands the "
                + "converter is never called."),

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

            new Entry(BlockExpansionFailed, NexSeverity.Error,
                "A Block instance could not be expanded, so its content is missing from the screen.",
                "The message names the cause - a missing definition, a reference cycle, an empty " +
                "definition, or an expansion budget. Fix it and recompile; the screen compiled " +
                "without that Block's elements."),

            new Entry(BlockExpansionIncomplete, NexSeverity.Warning,
                "A Block instance expanded, but part of what it declares was not applied.",
                "Usually a slot rule or an override that no longer matches the definition. The " +
                "message names which; the rest of the Block is present."),

            new Entry(BlockExpansionNote, NexSeverity.Suggestion,
                "A Block instance expanded with a detail worth knowing.",
                "Either its definition was recovered by id after its GUID stopped resolving - " +
                "re-save the screen to write the new GUID back - or a variant rule needed canvas " +
                "context a headless compile does not have."),

            new Entry(FragmentDefinitionMissing, NexSeverity.Error,
                "An element composes a Fragment whose definition asset could not be found.",
                "Restore the Fragment asset, or remove the Fragment from the element. Its " +
                "properties were not applied, so the element compiled without them."),

            new Entry(FragmentIncompatible, NexSeverity.Warning,
                "A Fragment was composed onto an element type it does not declare as compatible.",
                "It was applied anyway. Either add the element type to the Fragment's compatible " +
                "list, or use a Fragment intended for this element."),

            new Entry(FragmentShadowsAuthoredProperty, NexSeverity.Suggestion,
                "A Fragment sets a property that is also set directly on the element.",
                "The Fragment wins, because the stack composes on top of the element. Remove one " +
                "of the two so the value has a single source."),

            new Entry(StateOverrideNotCarried, NexSeverity.Warning,
                "A state changes something the compiled program cannot carry.",
                "Either the target element no longer exists, or the override sets an asset " +
                "reference - the content hash deliberately excludes asset identity. The rest of " +
                "the state compiled; the message names which override was dropped."),

            new Entry(ResponsiveOverrideNotCarried, NexSeverity.Warning,
                "A responsive rule could not be compiled, or one of its overrides could not.",
                "Either the rule can never match (a maximum resolution below its minimum, or a " +
                "duplicate id), or the override names a missing element or sets an asset " +
                "reference. The message names which; the rest of the rule compiled."),

            new Entry(LocalizationKeyConflict, NexSeverity.Warning,
                "An element is linked to two different localization keys.",
                "The key can be set on the element itself or in the screen's link table. The " +
                "element's own key wins; remove the other so the value has a single source - the " +
                "losing one still shows in the inspector and looks like it is doing something."),

            new Entry(PartOverrideNotCarried, NexSeverity.Warning,
                "A nudge to a control's internal part was not compiled.",
                "Either the element type has no part by that name, or the part is preview-only - "
                + "the canvas draws it but no backend builds an object for it. The authoring value "
                + "is preserved; the message names which case."),

            new Entry(PartNotBuilt, NexSeverity.Warning,
                "A compiled part nudge found no such part on the built control.",
                "The compiled uGUI builder assembles a leaner control than the prefab writer and "
                + "does not create every part the registry describes. The nudge is in the program "
                + "and applies as soon as the builder makes that part."),

            new Entry(StateChannelUnsupported, NexSeverity.Warning,
                "A state changes a property this backend did not apply.",
                "Either the backend does not handle that property, or the target node has no " +
                "component that carries it. The value is in the compiled program and the state's other properties were " +
                "applied. Reported once per property per screen rather than once per node, so a " +
                "grid of slots does not bury the message."),

            new Entry(AppearanceNotCarried, NexSeverity.Warning,
                "An appearance effect was authored that the compiled program cannot carry.",
                "Material and gradient are asset references, and the content hash deliberately " +
                "excludes asset identity. Bake the look into a sprite, or apply the material at " +
                "runtime through a component."),

            new Entry(FeatureCarriedNotApplied, NexSeverity.Suggestion,
                "The compiled screen carries an authored feature this backend does not act on.",
                "Not an error and not data loss - the feature is in the program and will work once " +
                "the backend supports it. Reported once per feature per screen so the reason a " +
                "setting appears to do nothing is written down somewhere."),

            new Entry(AppearanceFeatureUnsupported, NexSeverity.Warning,
                "The authored appearance uses an effect this backend cannot draw.",
                "The rest of the appearance was applied. The message names the effect and the " +
                "closest thing the backend can do instead."),

            new Entry(LayoutFeatureUnsupported, NexSeverity.Warning,
                "The authored layout uses something this backend cannot express.",
                "The rest of the layout was applied. Either accept the difference, or author the " +
                "screen within what the target backend supports - the message names the feature."),

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
