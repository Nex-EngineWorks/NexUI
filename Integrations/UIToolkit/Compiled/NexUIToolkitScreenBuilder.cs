using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Flow;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Overrides;
using emiteat.NexUI.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>What a UI Toolkit screen instance is wired to. All members are optional.</summary>
    public struct NexUIToolkitBuildOptions
    {
        /// <summary>Drives text and value bindings. Without it, authored literals are used as-is.</summary>
        public UIStateStore Store;

        /// <summary>Receives clicks from command-bound elements.</summary>
        public NexCommandRouter Router;

        /// <summary>Parent for the screen root. Required - a UI Toolkit tree has no scene to find.</summary>
        public VisualElement Parent;

        /// <summary>Supplies the converters named by binding metadata.</summary>
        public UIBindingConverterRegistry Converters;

        /// <summary>Resolves compiled motion ids to runtime presets.</summary>
        public UIMotionRegistryAsset MotionRegistry;

        /// <summary>Optional custom player. Defaults to BuiltInMotionPlayer when a registry exists.</summary>
        public IUIMotionPlayer MotionPlayer;
    }

    /// <summary>
    /// Builds a compiled screen into a live UI Toolkit element tree.
    /// </summary>
    /// <remarks>
    /// The second backend, consuming the same <see cref="NexScreenProgram"/> as the uGUI one. That
    /// is the whole claim the product makes - the author never picked a backend - and until this
    /// existed the claim was a design intention with one implementation behind it.
    ///
    /// Deliberately the same shape as the uGUI builder: a single forward pass over the node array,
    /// no lookups, no reflection, no decision the compiler could have made instead. Everything
    /// conditional was resolved at compile time, so a screen that compiled cannot fail halfway
    /// through construction on a player device.
    ///
    /// The interesting differences are three. A control here <em>is</em> the element rather than a
    /// component added to one, so the control decision happens before the element exists. Elements
    /// are managed objects rather than <c>UnityEngine.Object</c>s, so teardown unhooks callbacks
    /// instead of destroying. And style classes are native, so the compiled program's class list is
    /// applied here where uGUI can only report that it carried it.
    /// </remarks>
    public static class NexUIToolkitScreenBuilder
    {
        public static NexUIToolkitScreenRuntime Build(NexScreenProgram program,
            NexUIToolkitBuildOptions options, NexDiagnosticBag diagnostics = null)
        {
            if (program == null)
            {
                diagnostics?.Add(NexDiagnosticCodes.NoDocument, default, "No compiled screen was supplied.");
                return null;
            }

            if (program.CompilerVersion != NexScreenProgram.CurrentCompilerVersion)
            {
                diagnostics?.Add(NexDiagnosticCodes.ProgramSchemaMismatch,
                    new NexSourceLocation(program.ScreenId),
                    "Screen '" + program.ScreenId + "' was compiled by version " + program.CompilerVersion +
                    "; this runtime expects " + NexScreenProgram.CurrentCompilerVersion + ".");
                return null;
            }

            var root = CreateRoot(program);
            options.Parent?.Add(root);

            var sourceMap = new NexRuntimeSourceMap(program);
            var runtime = new NexUIToolkitScreenRuntime(program, root, sourceMap);

            var surface = new NexUIToolkitScreenSurface(sourceMap);
            var overrides = new NexOverrideLedger(program);
            runtime.Overrides = overrides;
            runtime.Surface = surface;

            var interactions = new NexInteractionRuntime(program, options.Router,
                new NexUIToolkitStateAccess(options.Store), surface, null, overrides);
            if (diagnostics != null) interactions.DiagnosticRaised += d => diagnostics.Add(d);
            runtime.AttachInteractions(interactions);

            // Before the build loop, so the report reads as a property of the screen rather than as
            // something that happened part-way through constructing it.
            var motionPlayer = options.MotionPlayer ??
                               (options.MotionRegistry != null ? new BuiltInMotionPlayer() : null);
            NexUIToolkitCarriedFeatureReport.Report(program, diagnostics,
                options.MotionRegistry != null && motionPlayer != null);

            var nodes = program.Nodes;
            var built = new VisualElement[nodes.Length];

            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];

                // The compiler guarantees a parent precedes its children, so this is always already
                // built. A negative or forward index would have failed validation.
                var parent = node.ParentIndex >= 0 ? built[node.ParentIndex] : root;
                var parentSize = node.ParentIndex >= 0
                    ? nodes[node.ParentIndex].Rect.size
                    : program.ReferenceResolution;
                var parentOrigin = node.ParentIndex >= 0
                    ? nodes[node.ParentIndex].Rect.position
                    : Vector2.zero;

                var element = CreateNode(runtime, node, parentOrigin, parentSize);
                built[i] = element;
                parent.Add(element);
                sourceMap.Register(i, element);

                var authoringPath = program.SourceMap.PathOfIndex(i);
                if (string.IsNullOrEmpty(authoringPath)) authoringPath = node.Name;

                NexUIToolkitStyleApplier.ApplyLayout(node, element, authoringPath, diagnostics);
                NexUIToolkitStyleApplier.ApplyAppearance(node, element, authoringPath, diagnostics);

                ApplyStyleClasses(node, element);
                WireText(runtime, node, element, options, authoringPath, i, overrides);
                WireValue(runtime, node, element, options, authoringPath, i, overrides);
                WireCommand(runtime, program, node, element, options, authoringPath);
                WireStateBindings(runtime, node, element, options, authoringPath, i, overrides);
                WireInteractionTriggers(runtime, interactions, i, element, node);
                WireAccessibility(node, element);
                WireMotion(runtime, node, element, options.MotionRegistry, motionPlayer,
                    authoringPath, diagnostics);

                // After the text wiring: typography is an override layer, so the base font size and
                // colour have to be in place before it runs.
                NexUIToolkitStyleApplier.ApplyTypography(node, element, authoringPath, diagnostics);

                if (!node.Visible) element.style.display = DisplayStyle.None;
            }

            // Part nudges are deltas from where the control put its parts, so they land while that
            // is still the current geometry - and before the condition applier snapshots anything.
            NexUIToolkitPartApplier.Apply(program, built, diagnostics);

            runtime.Conditions = new NexUIToolkitConditionApplier(program, built, diagnostics);
            runtime.Conditions.ApplyInitial(root);
            runtime.PlayEntryMotions();

            // The panel's size is not known until it has been laid out once, and a responsive rule
            // asked about the wrong size is worse than one asked late. uGUI has no equivalent
            // because a Canvas is sized before its children are built.
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var size = evt.newRect.size;
                if (size.x <= 0f || size.y <= 0f) return;

                runtime.Conditions.SetViewport(
                    new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y)),
                    runtime.Conditions.CurrentInputMode);
            });

            // Only once the whole tree exists: an OnShow rule that hides or retargets another
            // element must not run while that element is still being constructed.
            runtime.RaiseShow();

            return runtime;
        }

        private static void WireMotion(NexUIToolkitScreenRuntime runtime, in NexNodeProgram node,
            VisualElement element, UIMotionRegistryAsset registry, IUIMotionPlayer player,
            string authoringPath, NexDiagnosticBag diagnostics)
        {
            if (node.Motion.IsEmpty || registry == null || player == null) return;

            if (!registry.TryGet(node.Motion.MotionId, out var preset))
            {
                diagnostics?.Add(NexDiagnosticCodes.FeatureCarriedNotApplied,
                    new NexSourceLocation(runtime.ScreenId, node.NodeId, authoringPath, "Motion"),
                    "Motion '" + node.Motion.MotionId + "' could not be resolved by the runtime registry.");
                return;
            }

            var handle = new UIToolkitElementHandle(element, node.NodeId);
            runtime.AttachMotion(new CompiledMotionBinding(handle, preset, player,
                node.Motion.InitialVariant, node.Motion.AnimateVariant, node.Motion.ExitVariant,
                node.Motion.HoverVariant, node.Motion.PressedVariant, node.Motion.FocusVariant).Attach());
        }

        // ---- construction ---------------------------------------------------

        private static VisualElement CreateRoot(NexScreenProgram program)
        {
            var root = new VisualElement { name = "NexScreen:" + program.ScreenId };

            // The root fills its parent rather than taking the reference resolution as a fixed
            // size. The reference resolution is what the author designed against; what the screen
            // is displayed in is the panel, and pinning the root to the design size would letterbox
            // every screen on every device that is not exactly 1920x1080.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;
            return root;
        }

        /// <summary>
        /// Builds the element for one node - a control when the node asks for one, else a plain
        /// element or a label.
        /// </summary>
        /// <remarks>
        /// The control decision happens here rather than after, unlike uGUI where a control is a
        /// component added to an existing object. That is why this returns the element instead of
        /// mutating one: in UI Toolkit a Slider is not a VisualElement with a Slider on it.
        /// </remarks>
        private static VisualElement CreateNode(NexUIToolkitScreenRuntime runtime,
            in NexNodeProgram node, Vector2 parentOrigin, Vector2 parentSize)
        {
            var element = NexUIToolkitControls.Create(node, out var value, out var text);

            if (value != null)
            {
                runtime.ValueHandles[node.NodeId] = value;
                runtime.Track(new NexDisposable(value.Dispose));
            }
            if (text != null)
            {
                runtime.TextHandles[node.NodeId] = text;
                runtime.Track(new NexDisposable(text.Dispose));
            }

            if (element == null)
            {
                // A node that carries text and no control is a label. Everything else is a plain
                // box - which is also what an image node is, since a background image is a style.
                element = node.Kind == NexNodeKind.Label || !string.IsNullOrEmpty(node.Text)
                    ? new Label(node.Text ?? string.Empty)
                    : new VisualElement();
            }
            else if (element is TextElement textElement && !string.IsNullOrEmpty(node.Text))
            {
                textElement.text = node.Text;
            }

            element.name = node.Name;
            element.style.backgroundColor = node.Tint;

            if (element is TextElement labelled)
            {
                labelled.style.color = node.TextColor;
                if (node.FontSize > 0) labelled.style.fontSize = node.FontSize;
            }

            ApplyPlacement(element, node, parentOrigin, parentSize);
            return element;
        }

        /// <summary>
        /// Places a node inside its parent, honouring the author's anchor or resize constraints.
        /// </summary>
        /// <remarks>
        /// Absolute by default, because the authoring model's canvas is absolute and a node whose
        /// parent arranges children has that decided by the parent's layout instead - which is why
        /// this is skipped for those.
        ///
        /// The authoring origin is the parent's top-left with y growing down, which is also UI
        /// Toolkit's, so there is no flip here - the one place this backend is simpler than uGUI.
        /// </remarks>
        private static void ApplyPlacement(VisualElement element, in NexNodeProgram node,
            Vector2 parentOrigin, Vector2 parentSize)
        {
            var local = node.Rect.position - parentOrigin;
            var size = node.Rect.size;
            var style = element.style;

            style.position = Position.Absolute;

            if (node.Layout.PinsToParent)
            {
                ApplyConstraints(style, node.Layout, local, size, parentSize);
                return;
            }

            if (node.Anchor == NexAnchor.Stretch)
            {
                style.left = local.x;
                style.top = local.y;
                style.right = parentSize.x - (local.x + size.x);
                style.bottom = parentSize.y - (local.y + size.y);
                return;
            }

            style.left = local.x;
            style.top = local.y;
            style.width = size.x;
            style.height = size.y;
        }

        /// <summary>
        /// Expresses a parent-resize constraint as the edge set UI Toolkit understands.
        /// </summary>
        /// <remarks>
        /// Start pins the near edge, End the far edge, Center leaves both free and centres by
        /// margin, Scale pins both as percentages so the element grows with the parent. uGUI does
        /// the same thing through anchor pairs; here the vocabulary is edges, which is why this is
        /// a separate function rather than shared code.
        /// </remarks>
        private static void ApplyConstraints(IStyle style, in NexLayoutProgram layout,
            Vector2 local, Vector2 size, Vector2 parentSize)
        {
            ApplyAxis(layout.HorizontalConstraint, local.x, size.x, parentSize.x,
                v => style.left = v, v => style.right = v, v => style.width = v,
                centre: () =>
                {
                    style.width = size.x;
                    style.left = StyleKeyword.Auto;
                    style.right = StyleKeyword.Auto;
                });

            ApplyAxis(layout.VerticalConstraint, local.y, size.y, parentSize.y,
                v => style.top = v, v => style.bottom = v, v => style.height = v,
                centre: () =>
                {
                    style.height = size.y;
                    style.top = StyleKeyword.Auto;
                    style.bottom = StyleKeyword.Auto;
                });
        }

        private static void ApplyAxis(NexConstraintMode mode, float near, float extent,
            float parentExtent, Action<StyleLength> setNear, Action<StyleLength> setFar,
            Action<StyleLength> setSize, Action centre)
        {
            switch (mode)
            {
                case NexConstraintMode.End:
                    setFar(parentExtent - (near + extent));
                    setSize(extent);
                    return;
                case NexConstraintMode.Center:
                    centre();
                    return;
                case NexConstraintMode.Scale:
                    // A zero-extent parent has no proportion to preserve, and dividing by it would
                    // produce a NaN percentage that makes the element vanish rather than misplace it.
                    if (Mathf.Approximately(parentExtent, 0f)) goto default;

                    setNear(Percent(near / parentExtent));
                    setFar(Percent((parentExtent - (near + extent)) / parentExtent));
                    setSize(StyleKeyword.Auto);
                    return;
                default:
                    setNear(near);
                    setSize(extent);
                    return;
            }
        }

        private static StyleLength Percent(float fraction)
            => new StyleLength(new Length(fraction * 100f, LengthUnit.Percent));

        /// <summary>
        /// Applies the compiled style classes, which UI Toolkit has natively.
        /// </summary>
        /// <remarks>
        /// The one place this backend does something uGUI can only report. Order is preserved
        /// rather than sorted, because a later class overriding an earlier one is how cascading
        /// works and the compiler kept the author's order for exactly this consumer.
        /// </remarks>
        private static void ApplyStyleClasses(in NexNodeProgram node, VisualElement element)
        {
            var classes = node.Style.Classes;
            if (classes == null) return;

            for (int i = 0; i < classes.Length; i++)
                if (!string.IsNullOrEmpty(classes[i])) element.AddToClassList(classes[i]);
        }

        private static void WireAccessibility(in NexNodeProgram node, VisualElement element)
        {
            if (!string.IsNullOrEmpty(node.AccessibilityLabel))
                element.tooltip = node.AccessibilityLabel;

            if (node.IsFocusable && element is Focusable focusable)
                focusable.focusable = true;
        }

        // ---- bindings -------------------------------------------------------

        private static void WireText(NexUIToolkitScreenRuntime runtime, in NexNodeProgram node,
            VisualElement element, NexUIToolkitBuildOptions options, string authoringPath,
            int nodeIndex, NexOverrideLedger overrides)
        {
            if (string.IsNullOrEmpty(node.TextBindingKey) || options.Store == null) return;

            var key = node.TextBindingKey;
            var converter = ResolveConverter(node.TextConverterKey, options, authoringPath);

            // A text control owns its own text, so writing into its label child would be undone the
            // moment the field redraws. Nodes that have one are bound through the handle.
            if (runtime.TextHandles.TryGetValue(node.NodeId, out var handle))
            {
                WireTextControl(runtime, node, handle, options, authoringPath, nodeIndex, overrides, converter);
                return;
            }

            var target = element as TextElement ?? element.Q<TextElement>();
            if (target == null) return;

            runtime.Track(options.Store.Watch<object>(key, value =>
            {
                var converted = converter != null ? converter.Convert(value) : value;
                var next = converted != null ? converted.ToString() : string.Empty;

                overrides?.Record(nodeIndex, NexOverrideProperty.Text,
                    NexOverrideSource.Binding, next, key);

                if (NexFlowTrace.IsEnabled)
                {
                    using (var scope = NexFlowTrace.Begin(key))
                        scope.Step(authoringPath, "Text", NexFlowStatus.Ok, target.text + " → " + next);
                }

                target.text = next;
            }));
        }

        private static void WireTextControl(NexUIToolkitScreenRuntime runtime, in NexNodeProgram node,
            INexTextHandle control, NexUIToolkitBuildOptions options, string authoringPath,
            int nodeIndex, NexOverrideLedger overrides, IUIBindingConverter converter)
        {
            var key = node.TextBindingKey;
            var store = options.Store;

            if (node.TextBindingMode != UIBindingMode.OneWayToSource)
                runtime.Track(store.Watch<object>(key, value =>
                {
                    var converted = converter != null ? converter.Convert(value) : value;
                    var next = converted != null ? converted.ToString() : string.Empty;

                    overrides?.Record(nodeIndex, NexOverrideProperty.Text,
                        NexOverrideSource.Binding, next, key);
                    control.Text = next;
                }));

            // Write-back is gated on the mode and on the node being editable, so a mis-authored
            // screen degrades to one-way rather than persisting edits the author did not intend.
            if (node.TextBindingMode == UIBindingMode.OneWay || !node.IsUserEditable) return;

            void OnUserChanged(string value) => store.Set(key, value);

            control.UserChanged += OnUserChanged;
            runtime.Track(new NexDisposable(() => control.UserChanged -= OnUserChanged));
        }

        private static void WireValue(NexUIToolkitScreenRuntime runtime, in NexNodeProgram node,
            VisualElement element, NexUIToolkitBuildOptions options, string authoringPath,
            int nodeIndex, NexOverrideLedger overrides)
        {
            if (string.IsNullOrEmpty(node.ValueBindingKey) || options.Store == null) return;
            if (!runtime.ValueHandles.TryGetValue(node.NodeId, out var control)) return;

            var key = node.ValueBindingKey;
            var converter = ResolveConverter(node.ValueConverterKey, options, authoringPath);
            var store = options.Store;

            if (node.ValueBindingMode != UIBindingMode.OneWayToSource)
                runtime.Track(store.Watch<object>(key, value =>
                {
                    var converted = converter != null ? converter.Convert(value) : value;
                    var next = ToFloat(converted);

                    // Recorded under Text, the way the uGUI backend records it: the ledger tracks
                    // "what is this element showing", and a slider's number is that. A separate
                    // Value channel would have made the same question read differently per backend.
                    overrides?.Record(nodeIndex, NexOverrideProperty.Text,
                        NexOverrideSource.Binding, next.ToString("0.###"), key);
                    control.Value = next;
                }));

            if (!node.ValueWritesBack || !node.IsUserEditable) return;

            // Not recorded in the ledger, matching uGUI: the ledger answers "why does it say
            // that?", and "the user typed it" is the one answer nobody has to look up.
            void OnUserChanged(float value) => store.Set(key, value);

            control.UserChanged += OnUserChanged;
            runtime.Track(new NexDisposable(() => control.UserChanged -= OnUserChanged));
        }

        private static void WireStateBindings(NexUIToolkitScreenRuntime runtime, in NexNodeProgram node,
            VisualElement element, NexUIToolkitBuildOptions options, string authoringPath,
            int nodeIndex, NexOverrideLedger overrides)
        {
            var store = options.Store;
            if (store == null) return;

            if (!string.IsNullOrEmpty(node.VisibilityBindingKey))
            {
                var key = node.VisibilityBindingKey;
                runtime.Track(store.Watch<object>(key, value =>
                {
                    var visible = ToBool(value);
                    overrides?.Record(nodeIndex, NexOverrideProperty.Visible,
                        NexOverrideSource.Binding, visible ? "true" : "false", key);
                    element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                }));
            }

            if (!string.IsNullOrEmpty(node.InteractableBindingKey))
            {
                var key = node.InteractableBindingKey;
                runtime.Track(store.Watch<object>(key, value => element.SetEnabled(ToBool(value))));
            }

            if (!string.IsNullOrEmpty(node.ClassBindingKey))
            {
                var key = node.ClassBindingKey;
                string applied = null;

                // Only the bound class is swapped. The compiled class list is what the element
                // *is*; the binding adds one more that changes with state, and removing everything
                // would take the authored classes with it.
                runtime.Track(store.Watch<object>(key, value =>
                {
                    var next = value != null ? value.ToString() : string.Empty;
                    if (!string.IsNullOrEmpty(applied)) element.RemoveFromClassList(applied);
                    if (!string.IsNullOrEmpty(next)) element.AddToClassList(next);
                    applied = next;
                }));
            }
        }

        private static void WireCommand(NexUIToolkitScreenRuntime runtime, NexScreenProgram program,
            in NexNodeProgram node, VisualElement element, NexUIToolkitBuildOptions options,
            string authoringPath)
        {
            if (string.IsNullOrEmpty(node.CommandId) || options.Router == null) return;

            var commandId = node.CommandId;
            var nodeId = node.NodeId;
            var screenId = program.ScreenId;
            var router = options.Router;
            var origin = screenId + "/" + authoringPath;

            void OnClick(ClickEvent evt)
            {
                using (var scope = NexFlowTrace.Begin(origin))
                {
                    scope.Step(authoringPath, "Pointer.Click");
                    scope.Step(authoringPath, "Trigger.OnClick");
                    scope.Step("Command." + commandId, "Dispatch");

                    var result = router.Dispatch(new NexCommandContext
                    {
                        CommandId = commandId,
                        SenderPath = authoringPath,
                        SenderNodeId = nodeId,
                        ScreenId = screenId
                    });

                    if (result.Handled) scope.Step("Handler", "Invoke");
                    else scope.Failed("Handler", "Invoke",
                        result.Diagnostic != null ? result.Diagnostic.Code : NexDiagnosticCodes.NoCommandHandler,
                        result.Diagnostic != null ? result.Diagnostic.Message : null);
                }
            }

            element.RegisterCallback<ClickEvent>(OnClick);
            runtime.Track(new NexDisposable(() => element.UnregisterCallback<ClickEvent>(OnClick)));
        }

        // ---- interaction ----------------------------------------------------

        /// <summary>
        /// Registers the event callbacks the screen's authored rules actually need.
        /// </summary>
        /// <remarks>
        /// Only what is authored, node by node. UI Toolkit's events propagate whether or not anyone
        /// listens, so a blanket registration would cost a delegate per node per trigger on every
        /// screen to serve the few that use them.
        ///
        /// Long press and double click have no UI Toolkit event and are reported by the compiler's
        /// own capability check rather than silently doing nothing here.
        /// </remarks>
        private static void WireInteractionTriggers(NexUIToolkitScreenRuntime runtime,
            NexInteractionRuntime interactions, int nodeIndex, VisualElement element,
            in NexNodeProgram node)
        {
            if (interactions.WantsClickListener(nodeIndex))
            {
                void OnClick(ClickEvent evt) => interactions.Fire(nodeIndex, NexTrigger.OnClick);
                element.RegisterCallback<ClickEvent>(OnClick);
                runtime.Track(new NexDisposable(() => element.UnregisterCallback<ClickEvent>(OnClick)));
            }

            Relay<PointerEnterEvent>(runtime, interactions, nodeIndex, element, NexTrigger.OnPointerEnter);
            Relay<PointerLeaveEvent>(runtime, interactions, nodeIndex, element, NexTrigger.OnPointerExit);
            Relay<PointerDownEvent>(runtime, interactions, nodeIndex, element, NexTrigger.OnPointerDown);
            Relay<PointerUpEvent>(runtime, interactions, nodeIndex, element, NexTrigger.OnPointerUp);
            Relay<NavigationSubmitEvent>(runtime, interactions, nodeIndex, element, NexTrigger.OnSubmit);
            Relay<NavigationCancelEvent>(runtime, interactions, nodeIndex, element, NexTrigger.OnCancel);
        }

        private static void Relay<TEvent>(NexUIToolkitScreenRuntime runtime,
            NexInteractionRuntime interactions, int nodeIndex, VisualElement element,
            NexTrigger trigger) where TEvent : EventBase<TEvent>, new()
        {
            if (!interactions.WantsListener(nodeIndex, trigger)) return;

            void Handler(TEvent evt) => interactions.Fire(nodeIndex, trigger);
            element.RegisterCallback<TEvent>(Handler);
            runtime.Track(new NexDisposable(() => element.UnregisterCallback<TEvent>(Handler)));
        }

        // ---- shared helpers -------------------------------------------------

        private static IUIBindingConverter ResolveConverter(string key,
            NexUIToolkitBuildOptions options, string authoringPath)
        {
            if (string.IsNullOrEmpty(key) || options.Converters == null) return null;
            return options.Converters.TryResolve(key, out var converter) ? converter : null;
        }

        private static float ToFloat(object value)
        {
            switch (value)
            {
                case null: return 0f;
                case float f: return f;
                case double d: return (float)d;
                case int i: return i;
                case long l: return l;
                case bool b: return b ? 1f : 0f;
                default:
                    return float.TryParse(value.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : 0f;
            }
        }

        private static bool ToBool(object value)
        {
            switch (value)
            {
                case null: return false;
                case bool b: return b;
                case float f: return f >= 0.5f;
                case double d: return d >= 0.5;
                case int i: return i != 0;
                default: return bool.TryParse(value.ToString(), out var parsed) && parsed;
            }
        }

        /// <summary>An <see cref="IDisposable"/> over a delegate, for the teardown list.</summary>
        private sealed class NexDisposable : IDisposable
        {
            private Action _dispose;

            public NexDisposable(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }
    }
}
