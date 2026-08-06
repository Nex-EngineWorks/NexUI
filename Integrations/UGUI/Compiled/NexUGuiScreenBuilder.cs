using System;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Flow;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.Overrides;
using emiteat.NexUI.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>What a screen instance is wired to. All members are optional.</summary>
    public struct NexScreenBuildOptions
    {
        /// <summary>Drives text bindings. Without it, authored literal text is used as-is.</summary>
        public UIStateStore Store;

        /// <summary>Receives clicks from command-bound elements.</summary>
        public NexCommandRouter Router;

        /// <summary>Parent for the screen root. Falls back to the first canvas in the scene.</summary>
        public Transform Parent;

        /// <summary>
        /// Supplies the converters named by binding metadata. Optional; without it a binding that
        /// names a converter falls back to the raw value and says so once.
        /// </summary>
        /// <remarks>
        /// Owned by the project rather than by NexUI. A converter is game logic - "hp to a bar
        /// colour", "seconds to mm:ss" - and baking a fixed set into the framework would mean
        /// every project either bends to it or ignores it.
        /// </remarks>
        public UIBindingConverterRegistry Converters;
    }

    /// <summary>
    /// Builds a compiled screen into live uGUI objects.
    /// </summary>
    /// <remarks>
    /// This is the whole uGUI backend for compiled screens, and it is deliberately dumb: a single
    /// forward pass over the node array with no lookups, no reflection and no decisions that the
    /// compiler could have made instead. Everything conditional - which authoring type became a
    /// Button, whether a binding is valid, what the reference resolution is - was resolved at
    /// compile time, so a screen that compiled cannot fail halfway through construction on a
    /// player device.
    ///
    /// Instantiating objects directly rather than from a prefab is what lets one compiled asset
    /// serve every backend. The prefab path still exists for teams who want a plain uGUI artifact
    /// they can hand-edit; that is an export, not the runtime path.
    /// </remarks>
    public static class NexUGuiScreenBuilder
    {
        public static NexScreenRuntime Build(NexScreenProgram program, NexScreenBuildOptions options,
            NexDiagnosticBag diagnostics = null)
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

            var parent = options.Parent != null ? options.Parent : FindCanvas();
            var root = CreateRoot(program, parent);
            var sourceMap = new NexRuntimeSourceMap(program);
            var runtime = new NexScreenRuntime(program, root, sourceMap);

            var surface = new NexUGuiScreenSurface(sourceMap);
            var overrides = new NexOverrideLedger(program);
            runtime.Overrides = overrides;
            runtime._surface = surface;

            var interactions = new NexInteractionRuntime(program, options.Router,
                new NexStateStoreAccess(options.Store), surface, null, overrides);
            if (diagnostics != null) interactions.DiagnosticRaised += d => diagnostics.Add(d);
            runtime.AttachInteractions(interactions);

            var nodes = program.Nodes;
            var built = new RectTransform[nodes.Length];

            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];

                // The compiler guarantees a parent precedes its children, so this is always
                // already built. A negative or forward index would have failed validation.
                var parentRect = node.ParentIndex >= 0 ? built[node.ParentIndex] : root.transform as RectTransform;
                var parentSize = node.ParentIndex >= 0
                    ? nodes[node.ParentIndex].Rect.size
                    : program.ReferenceResolution;
                var parentOrigin = node.ParentIndex >= 0
                    ? nodes[node.ParentIndex].Rect.position
                    : Vector2.zero;

                var rect = CreateNode(node, parentRect, parentOrigin, parentSize);
                built[i] = rect;
                sourceMap.Register(i, rect.gameObject);

                var authoringPath = program.SourceMap.PathOfIndex(i);
                if (string.IsNullOrEmpty(authoringPath)) authoringPath = node.Name;

                WireText(runtime, node, rect, options, authoringPath, i, overrides);
                WireCommand(runtime, program, node, rect, options, authoringPath);
                WireStateBindings(runtime, node, rect, options, authoringPath, i, overrides);
                WireInteractionTriggers(runtime, interactions, i, rect, nodes[i]);
                WireOverlay(runtime, interactions, i, rect, nodes[i]);
                WireAccessibility(node, rect);

                rect.gameObject.SetActive(node.Visible);
            }

            // After the loop: chaining navigation needs every selectable to exist first.
            NexAccessibility.ApplyExplicitNavigation(root);

            // Only screens that actually park a rule mid-sequence get a per-frame pump.
            if (!interactions.IsEmpty && program.Interactions.HasDelays())
                NexScreenTicker.Attach(root, interactions);

            // Only once the whole hierarchy exists: an OnShow rule that hides or retargets another
            // element must not run while that element is still being constructed.
            runtime.RaiseShow();

            return runtime;
        }

        /// <summary>
        /// Carries the node's semantics onto the built object.
        /// </summary>
        /// <remarks>
        /// Only for nodes that have something to say. A grouping panel with no role and no label
        /// would add a component to every container on the screen to record that there is nothing
        /// to record - cost with no consumer, which is exactly what the pay-for-what-you-use rule
        /// is about.
        /// </remarks>
        private static void WireAccessibility(in NexNodeProgram node, RectTransform rect)
        {
            if (node.Role == Accessibility.AccessibilityRole.None
                && string.IsNullOrEmpty(node.AccessibilityLabel)
                && !node.IsFocusable)
                return;

            rect.gameObject.AddComponent<NexAccessibleNode>().Apply(node);
        }

        // ---- construction ---------------------------------------------------

        private static GameObject CreateRoot(NexScreenProgram program, Transform parent)
        {
            var root = new GameObject("NexScreen:" + program.ScreenId, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            if (parent != null) rect.SetParent(parent, false);

            // The root always fills its parent: the authored rects inside are absolute against
            // the reference resolution, and a root that did not stretch would offset all of them.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root;
        }

        private static RectTransform CreateNode(NexNodeProgram node, RectTransform parent,
            Vector2 parentOrigin, Vector2 parentSize)
        {
            var go = new GameObject(string.IsNullOrEmpty(node.Name) ? node.Kind.ToString() : node.Name,
                typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            ApplyPlacement(rect, node, parentOrigin, parentSize);

            switch (node.Kind)
            {
                case NexNodeKind.Panel:
                    break;

                case NexNodeKind.Image:
                    go.AddComponent<Image>().color = node.Tint;
                    break;

                case NexNodeKind.Label:
                    CreateText(go, node, stretch: true);
                    break;

                case NexNodeKind.Button:
                    go.AddComponent<Image>().color = node.Tint;
                    var button = go.AddComponent<Button>();
                    button.targetGraphic = go.GetComponent<Image>();

                    // The label is a child rather than a component on the button itself so the
                    // two can be styled and bound independently, which is what authors expect
                    // from every other UI tool.
                    var labelGo = new GameObject("Label", typeof(RectTransform));
                    labelGo.transform.SetParent(rect, false);
                    CreateText(labelGo, node, stretch: true);
                    break;
            }

            return rect;
        }

        private static TextMeshProUGUI CreateText(GameObject go, NexNodeProgram node, bool stretch)
        {
            var rect = (RectTransform)go.transform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = node.Text ?? string.Empty;
            text.color = node.TextColor;
            text.fontSize = node.FontSize > 0 ? node.FontSize : 14;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false; // The button behind it must keep receiving the click.
            return text;
        }

        /// <summary>
        /// Places a node from its absolute authoring rect (top-left origin, y down) into uGUI's
        /// anchor space (bottom-left origin, y up).
        /// </summary>
        /// <remarks>
        /// Both the parent's authored origin and size come from the compiled program, not from the
        /// live hierarchy, so placement does not depend on Unity having laid anything out yet.
        /// That is what makes the build a single pass and makes the result identical whether the
        /// screen is built in a player, in a test with no canvas, or during a headless bake.
        /// </remarks>
        private static void ApplyPlacement(RectTransform rect, NexNodeProgram node,
            Vector2 parentOrigin, Vector2 parentSize)
        {
            var local = node.Rect.position - parentOrigin;
            var size = node.Rect.size;

            if (node.Anchor == NexAnchor.Stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(local.x, parentSize.y - local.y - size.y);
                rect.offsetMax = new Vector2(local.x + size.x - parentSize.x, -local.y);
                return;
            }

            var anchor = AnchorPoint(node.Anchor);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 1f); // Element's own top-left, matching the authoring origin.
            rect.sizeDelta = size;

            var topLeftInParent = new Vector2(local.x, parentSize.y - local.y);
            rect.anchoredPosition = topLeftInParent - new Vector2(anchor.x * parentSize.x, anchor.y * parentSize.y);
        }

        private static Vector2 AnchorPoint(NexAnchor anchor)
        {
            switch (anchor)
            {
                case NexAnchor.Top: return new Vector2(0.5f, 1f);
                case NexAnchor.TopRight: return new Vector2(1f, 1f);
                case NexAnchor.Left: return new Vector2(0f, 0.5f);
                case NexAnchor.Center: return new Vector2(0.5f, 0.5f);
                case NexAnchor.Right: return new Vector2(1f, 0.5f);
                case NexAnchor.BottomLeft: return new Vector2(0f, 0f);
                case NexAnchor.Bottom: return new Vector2(0.5f, 0f);
                case NexAnchor.BottomRight: return new Vector2(1f, 0f);
                default: return new Vector2(0f, 1f); // TopLeft
            }
        }

        // ---- wiring ---------------------------------------------------------

        private static void WireText(NexScreenRuntime runtime, NexNodeProgram node, RectTransform rect,
            NexScreenBuildOptions options, string authoringPath, int nodeIndex, NexOverrideLedger overrides)
        {
            if (string.IsNullOrEmpty(node.TextBindingKey) || options.Store == null) return;

            var key = node.TextBindingKey;

            // Resolved once at build time, not per update. A converter that is not registered is
            // reported here rather than on every value change, which would bury the console.
            var converter = ResolveConverter(node.TextConverterKey, options, authoringPath);

            // An input field owns its text component, so writing to that directly would be undone
            // the moment the field redraws. Nodes that have a text control are bound through it;
            // everything else is a label and is written to directly.
            var textControl = NexUGuiControls.AttachText(rect.gameObject, node);
            if (textControl != null)
            {
                runtime.Track(new NexDisposable(textControl.Dispose));
                WireTextControl(runtime, node, textControl, options, authoringPath, nodeIndex, overrides, converter);
                return;
            }

            var text = rect.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null) return;

            runtime.Track(options.Store.Watch<object>(key, value =>
            {
                var converted = converter != null ? converter.Convert(value) : value;
                var next = converted != null ? converted.ToString() : string.Empty;

                // Recorded so "why does this label say that?" names the binding key rather than
                // leaving the author to guess which of the screen's bindings reached it.
                overrides?.Record(nodeIndex, NexOverrideProperty.Text,
                    NexOverrideSource.Binding, next, key);

                // The trace is what answers "who changed this label?" - the question that
                // otherwise costs an afternoon of breakpoints in a data-driven screen.
                if (NexFlowTrace.IsEnabled)
                {
                    using (var scope = NexFlowTrace.Begin(key))
                        scope.Step(authoringPath, "Text", NexFlowStatus.Ok, text.text + " → " + next);
                }

                text.text = next;
            }));
        }

        /// <summary>
        /// Binds an input field's text, in whichever direction the author asked for.
        /// </summary>
        /// <remarks>
        /// Mirrors <c>WireValue</c>'s shape, including the rule that write-back is gated on the
        /// mode <em>and</em> on the node being user-editable. Honouring both means a mis-authored
        /// screen degrades to one-way rather than surprising the author with edits they did not
        /// intend to persist.
        ///
        /// The converter is applied on the way in only. Converting on the way out would need the
        /// inverse function, which a one-way converter does not have - so what the user typed is
        /// what is stored, and the display formatting stays a display concern.
        /// </remarks>
        private static void WireTextControl(NexScreenRuntime runtime, NexNodeProgram node,
            INexTextHandle control, NexScreenBuildOptions options, string authoringPath,
            int nodeIndex, NexOverrideLedger overrides, State.IUIBindingConverter converter)
        {
            var key = node.TextBindingKey;

            if (node.TextBindingMode != State.UIBindingMode.OneWayToSource)
            {
                runtime.Track(options.Store.Watch<object>(key, value =>
                {
                    var converted = converter != null ? converter.Convert(value) : value;
                    var next = converted != null ? converted.ToString() : string.Empty;

                    overrides?.Record(nodeIndex, NexOverrideProperty.Text,
                        NexOverrideSource.Binding, next, key);

                    if (NexFlowTrace.IsEnabled)
                    {
                        using (var scope = NexFlowTrace.Begin(key))
                            scope.Step(authoringPath, "Text", NexFlowStatus.Ok, control.Text + " → " + next);
                    }

                    control.Text = next;
                }));
            }

            if (node.TextBindingMode == State.UIBindingMode.OneWay || !node.IsUserEditable) return;

            Action<string> writeBack = typed =>
            {
                if (NexFlowTrace.IsEnabled)
                {
                    using (var scope = NexFlowTrace.Begin(authoringPath))
                        scope.Step(key, "TextWriteBack", NexFlowStatus.Ok, typed);
                }

                options.Store.Set(key, typed);
            };

            control.UserChanged += writeBack;
            runtime.Track(new NexDisposable(() => control.UserChanged -= writeBack));
        }

        /// <summary>
        /// Wires the bindings that are not text or command: visibility and interactability.
        /// </summary>
        /// <remarks>
        /// Value and class bindings are deliberately absent. The compiled node kinds are panel,
        /// image, label and button - none of them holds a scalar, and there is no style-class
        /// system on the compiled uGUI path - so there is nothing to wire them to yet. The
        /// compiler reports that (NEX-BND-4009) rather than either side pretending otherwise;
        /// this method starts honouring them the moment the backend can build a control that
        /// carries a value.
        ///
        /// Both bindings below record into the override ledger for the same reason the text
        /// binding does: "why is this hidden?" has to name the key that hid it.
        /// </remarks>
        private static void WireStateBindings(NexScreenRuntime runtime, NexNodeProgram node,
            RectTransform rect, NexScreenBuildOptions options, string authoringPath, int nodeIndex,
            NexOverrideLedger overrides)
        {
            if (options.Store == null) return;

            if (!string.IsNullOrEmpty(node.VisibilityBindingKey))
            {
                var key = node.VisibilityBindingKey;
                var target = rect.gameObject;

                runtime.Track(options.Store.Watch<object>(key, value =>
                {
                    var visible = ToBool(value);
                    overrides?.Record(nodeIndex, NexOverrideProperty.Visible,
                        NexOverrideSource.Binding, visible ? "true" : "false", key);

                    if (NexFlowTrace.IsEnabled)
                    {
                        using (var scope = NexFlowTrace.Begin(key))
                            scope.Step(authoringPath, "Visible", NexFlowStatus.Ok, visible.ToString());
                    }

                    if (target != null) target.SetActive(visible);
                }));
            }

            WireValue(runtime, node, rect, options, authoringPath, nodeIndex, overrides);

            if (string.IsNullOrEmpty(node.InteractableBindingKey)) return;

            // Selectable rather than Button: the same binding has to keep working once the backend
            // can emit toggles and sliders, and every one of those is a Selectable.
            var selectable = rect.GetComponent<Selectable>();
            if (selectable == null) return;

            var interactableKey = node.InteractableBindingKey;
            runtime.Track(options.Store.Watch<object>(interactableKey, value =>
            {
                var interactable = ToBool(value);

                if (NexFlowTrace.IsEnabled)
                {
                    using (var scope = NexFlowTrace.Begin(interactableKey))
                        scope.Step(authoringPath, "Interactable", NexFlowStatus.Ok, interactable.ToString());
                }

                if (selectable != null) selectable.interactable = interactable;
            }));
        }

        /// <summary>
        /// Draws the node's vector path, replacing the rect fill it would otherwise have.
        /// </summary>
        /// <remarks>
        /// The rules live in <see cref="NexUGuiShapeApplier"/> so the Designer's prefab writer
        /// applies exactly the same ones. Keeping a second copy here is how the two paths drifted
        /// apart the first time.
        /// </remarks>
        private static void WireShape(in NexNodeProgram node, RectTransform rect)
        {
            if (!node.HasShape) return;
            NexUGuiShapeApplier.Apply(rect.gameObject, node.Shape);
        }

        /// <summary>
        /// Binds a control's value, in whichever direction the author asked for.
        /// </summary>
        /// <remarks>
        /// The control is attached here rather than during construction because whether a node
        /// needs one is a capability question, and only nodes that declare
        /// <see cref="NexNodeCapabilities.Value"/> pay for it.
        ///
        /// Write-back is gated on the mode <em>and</em> on the node being user-editable. The
        /// compiler already warns when those disagree; honouring both here means a mis-authored
        /// screen degrades to one-way rather than throwing at runtime.
        /// </remarks>
        private static void WireValue(NexScreenRuntime runtime, NexNodeProgram node, RectTransform rect,
            NexScreenBuildOptions options, string authoringPath, int nodeIndex, NexOverrideLedger overrides)
        {
            WireShape(node, rect);

            // Authored settings first: a control built below should come up already configured
            // rather than flickering from its defaults on the first frame.
            if (node.ControlProperties != null && node.ControlProperties.Length > 0)
                NexUGuiPropertyApplier.Apply(rect.gameObject, new NexProgramPropertySource(node));

            if (!node.HasValue) return;

            var control = NexUGuiControls.Attach(rect.gameObject, node);
            if (control == null) return;

            runtime.Track(new NexDisposable(control.Dispose));

            var key = node.ValueBindingKey;
            if (string.IsNullOrEmpty(key) || options.Store == null) return;

            var converter = ResolveConverter(node.ValueConverterKey, options, authoringPath);

            if (node.ValueBindingMode != State.UIBindingMode.OneWayToSource)
            {
                runtime.Track(options.Store.Watch<object>(key, value =>
                {
                    var converted = converter != null ? converter.Convert(value) : value;
                    var number = ToFloat(converted);

                    overrides?.Record(nodeIndex, NexOverrideProperty.Text,
                        NexOverrideSource.Binding, number.ToString("0.###"), key);

                    if (NexFlowTrace.IsEnabled)
                    {
                        using (var scope = NexFlowTrace.Begin(key))
                            scope.Step(authoringPath, "Value", NexFlowStatus.Ok, number.ToString("0.###"));
                    }

                    control.Value = number;
                }));
            }

            if (!node.ValueWritesBack || !node.IsUserEditable) return;

            control.UserChanged += number =>
            {
                object outgoing = number;
                if (converter != null)
                {
                    try { outgoing = converter.ConvertBack(number); }
                    catch (NotSupportedException)
                    {
                        // A one-way converter on a two-way binding. Reported once, and the raw
                        // number is written rather than dropping the user's edit on the floor.
                        Debug.LogWarning($"[NexUI] '{authoringPath}' writes back through converter "
                                         + $"'{node.ValueConverterKey}', which is one-way. The raw value is stored.");
                        converter = null;
                    }
                }

                if (NexFlowTrace.IsEnabled)
                {
                    using (var scope = NexFlowTrace.Begin(authoringPath))
                        scope.Step(key, "ValueWriteBack", NexFlowStatus.Ok, number.ToString("0.###"));
                }

                options.Store.Set(key, outgoing);
            };
        }

        /// <summary>Reads a bound value as a number, mirroring <see cref="ToBool"/>'s leniency.</summary>
        private static float ToFloat(object value)
        {
            switch (value)
            {
                case null: return 0f;
                case float f: return f;
                case double d: return (float)d;
                case int i: return i;
                case long l: return l;
                case bool flag: return flag ? 1f : 0f;
                case string s:
                    return float.TryParse(s, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
                default: return 0f;
            }
        }

        /// <summary>Adapts a plain callback to the disposable the runtime tracks.</summary>
        private sealed class NexDisposable : IDisposable
        {
            private Action _dispose;

            public NexDisposable(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }

        /// <summary>
        /// Looks up a converter, warning once when a named one is not registered.
        /// </summary>
        /// <remarks>
        /// A missing converter degrades to the raw value rather than dropping the binding. The
        /// label then shows something unformatted, which is visibly wrong and traceable to the
        /// warning - better than a blank element with no explanation anywhere.
        /// </remarks>
        private static IUIBindingConverter ResolveConverter(string key, NexScreenBuildOptions options,
            string authoringPath)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (options.Converters != null && options.Converters.TryResolve(key, out var converter))
                return converter;

            Debug.LogWarning($"[NexUI] '{authoringPath}' names binding converter '{key}', which is not "
                             + "registered. The raw value is shown instead.");
            return null;
        }

        /// <summary>
        /// Reads a bound value as a boolean.
        /// </summary>
        /// <remarks>
        /// State keys are loosely typed, and a screen binding visibility to a count ("show it when
        /// there are items") is a normal thing to author. Treating a non-zero number as true, and
        /// a non-null object as present, is what makes that work without a converter for the cases
        /// nobody would write one for.
        /// </remarks>
        private static bool ToBool(object value)
        {
            switch (value)
            {
                case null: return false;
                case bool flag: return flag;
                case int i: return i != 0;
                case long l: return l != 0L;
                case float f: return !Mathf.Approximately(f, 0f);
                case double d: return d != 0d;
                case string s: return !string.IsNullOrEmpty(s)
                                      && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase);
                default: return true;
            }
        }

        private static void WireCommand(NexScreenRuntime runtime, NexScreenProgram program,
            NexNodeProgram node, RectTransform rect, NexScreenBuildOptions options, string authoringPath)
        {
            if (string.IsNullOrEmpty(node.CommandId)) return;

            var button = rect.GetComponent<Button>();
            if (button == null) return; // The compiler already reported this as NEX-BND-4001.

            var commandId = node.CommandId;
            var nodeId = node.NodeId;
            var screenId = program.ScreenId;
            var router = options.Router;
            var origin = screenId + "/" + authoringPath;

            button.onClick.AddListener(() =>
            {
                using (var scope = NexFlowTrace.Begin(origin))
                {
                    scope.Step(authoringPath, "Pointer.Click");
                    scope.Step(authoringPath, "Trigger.OnClick");

                    if (router == null)
                    {
                        scope.Failed("Command." + commandId, "Dispatch", NexDiagnosticCodes.NoCommandHandler,
                            "No command router is wired to this screen.");
                        return;
                    }

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
            });

            runtime.Track(new ClickUnsubscribe(button));
        }

        /// <summary>
        /// Last-resort parent when the caller did not name one. Explicitly passing a parent is
        /// the supported path; this exists so a quick test or a sample does not need a bootstrap.
        /// </summary>
        /// <remarks>
        /// The only version fork in the compiled pipeline, and it is here because there is no
        /// single spelling that is warning-free on both supported versions:
        /// <c>FindAnyObjectByType</c> does not exist before 2023.1, and <c>FindObjectOfType</c> is
        /// deprecated from 2023.1 on. Shipping with warnings is not an option (Asset Store
        /// submissions must be warning-clean), so the fork stays until 2022.3 support is dropped.
        /// </remarks>
        /// <summary>
        /// Subscribes the node's backend events to the interaction engine.
        /// </summary>
        /// <remarks>
        /// Nothing is subscribed when the screen authored no rule for the trigger. The check is
        /// the compiled manifest, not a runtime search, so a screen with no interactions pays for
        /// no listeners at all - the "pay for what you use" rule applied at the event level.
        /// </remarks>
        /// <summary>
        /// Triggers raised by the event system rather than by a control's own callback.
        /// </summary>
        /// <remarks>
        /// <see cref="NexTrigger.OnClick"/> is absent: it comes from the Button, which already
        /// handles the press/release/still-inside logic that makes a click a click. Re-deriving it
        /// from pointer events would double-fire on every button in the project.
        /// </remarks>
        private static readonly NexTrigger[] RelayTriggers =
        {
            NexTrigger.OnPointerEnter, NexTrigger.OnPointerExit,
            NexTrigger.OnPointerDown, NexTrigger.OnPointerUp,
            NexTrigger.OnSubmit, NexTrigger.OnCancel,
            NexTrigger.OnLongPress, NexTrigger.OnDoubleClick,
            NexTrigger.OnDrop
        };

        /// <summary>
        /// Triggers that make a node a drag source, and so need the separate drag relay.
        /// </summary>
        /// <remarks>
        /// Kept apart from <see cref="RelayTriggers"/> because attaching the drag handlers is not
        /// free: uGUI picks the drag target by the presence of the interface, so a node that gets
        /// them without authoring a drag would capture gestures meant for a scroll view above it.
        /// </remarks>
        private static readonly NexTrigger[] DragTriggers =
        {
            NexTrigger.OnDragBegin, NexTrigger.OnDrag, NexTrigger.OnDragEnd
        };

        private static void WireInteractionTriggers(NexScreenRuntime runtime,
            NexInteractionRuntime interactions, int nodeIndex, RectTransform rect, in NexNodeProgram node)
        {
            if (interactions.WantsClickListener(nodeIndex))
            {
                var button = rect.GetComponent<Button>();
                if (button != null) // The compiler already reported a missing one as NEX-BND-4004.
                {
                    button.onClick.AddListener(() => interactions.Fire(nodeIndex, NexTrigger.OnClick));
                    runtime.Track(new ClickUnsubscribe(button));
                }
            }

            NXInteractionRelay relay = null;
            foreach (var trigger in RelayTriggers)
            {
                if (!interactions.WantsListener(nodeIndex, trigger)) continue;

                if (relay == null)
                {
                    relay = rect.gameObject.AddComponent<NXInteractionRelay>();

                    // Captured once outside the per-trigger loop: the relay reports which trigger
                    // fired, so one subscription serves all of them.
                    var index = nodeIndex;
                    relay.Triggered += fired => interactions.Fire(index, fired);
                    runtime.Track(new RelayTeardown(relay));
                }

                relay.Want(trigger);
            }

            NXInteractionDragRelay dragRelay = null;
            foreach (var trigger in DragTriggers)
            {
                if (!interactions.WantsListener(nodeIndex, trigger)) continue;

                if (dragRelay == null)
                {
                    dragRelay = rect.gameObject.AddComponent<NXInteractionDragRelay>();
                    ApplyDragVisual(dragRelay, node);

                    var index = nodeIndex;
                    dragRelay.Triggered += fired => interactions.Fire(index, fired);

                    // Publishing the source is not a trigger: it happens on every drag from this
                    // node so that whichever element receives the drop can identify what it caught.
                    dragRelay.DragSourceChanged += dragging =>
                    {
                        if (dragging) interactions.SetDragSource(index);
                        else interactions.ClearDragSource();
                    };

                    runtime.Track(new DragRelayTeardown(dragRelay));
                }

                dragRelay.Want(trigger);
            }
        }

        /// <summary>
        /// Attaches the component that owns an overlay's open/close life, and routes its close
        /// requests through the interaction engine.
        /// </summary>
        /// <remarks>
        /// The runtime components for these existed and nothing ever attached them on the compiled
        /// path, so a modal built from a compiled screen was a panel that sat there: no backdrop
        /// dismissal, no toast timeout, nothing.
        ///
        /// A close request is offered to the author first and honoured by default. Waiting for a
        /// rule that may not exist would leave a modal that can never be dismissed, which locks
        /// the screen - so the fallback is to close, and a rule that wants to confirm first simply
        /// listens.
        /// </remarks>
        private static void WireOverlay(NexScreenRuntime runtime, NexInteractionRuntime interactions,
            int nodeIndex, RectTransform rect, in NexNodeProgram node)
        {
            if (!node.IsOverlay) return;

            var authored = interactions != null && !interactions.IsEmpty &&
                           interactions.WantsListener(nodeIndex, NexTrigger.OnCloseRequested);

            switch (node.ControlId)
            {
                case "Modal":
                {
                    var modal = rect.GetComponent<NXModal>() ?? rect.gameObject.AddComponent<NXModal>();
                    var index = nodeIndex;

                    Action<string> onClose = _ =>
                    {
                        if (authored) interactions.Fire(index, NexTrigger.OnCloseRequested);
                        else modal.Close();
                    };

                    modal.CloseRequested += onClose;
                    runtime.Track(new NexDisposable(() => modal.CloseRequested -= onClose));
                    break;
                }

                case "Popover":
                    if (rect.GetComponent<NXPopover>() == null) rect.gameObject.AddComponent<NXPopover>();
                    break;

                case "Tooltip":
                    if (rect.GetComponent<NXTooltipPanel>() == null)
                        rect.gameObject.AddComponent<NXTooltipPanel>();
                    break;

                case "Toast":
                {
                    var toast = rect.GetComponent<NXToast>() ?? rect.gameObject.AddComponent<NXToast>();
                    var index = nodeIndex;

                    // A toast dismisses itself when its time runs out, so the trigger is a
                    // notification rather than a request - there is nothing left to refuse.
                    UnityEngine.Events.UnityAction onDismissed = () =>
                    {
                        if (authored) interactions.Fire(index, NexTrigger.OnCloseRequested);
                    };

                    toast.Dismissed.AddListener(onDismissed);
                    runtime.Track(new NexDisposable(() => toast.Dismissed.RemoveListener(onDismissed)));
                    break;
                }
            }
        }

        /// <summary>Detaches the relay on teardown, and stops it raising into a dead screen.</summary>
        private sealed class RelayTeardown : IDisposable
        {
            private NXInteractionRelay _relay;

            public RelayTeardown(NXInteractionRelay relay) => _relay = relay;

            public void Dispose()
            {
                if (_relay != null)
                {
                    // Disabled first: destruction is deferred outside the editor, and a relay left
                    // live for those frames would fire into a runtime that has already been torn
                    // down.
                    _relay.enabled = false;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(_relay);
                    else UnityEngine.Object.DestroyImmediate(_relay);
                }
                _relay = null;
            }
        }

        /// <summary>
        /// Reads the node's authored drag feedback out of the compiled property bag.
        /// </summary>
        /// <remarks>
        /// The property bag rather than new fields on <see cref="NexNodeProgram"/>: the bag already
        /// travels the compile path, already contributes to the content hash, and already carries
        /// keys the current schema does not know - so drag feedback needed no change to the program
        /// format at all.
        ///
        /// Matched by name rather than by index, like every other enum in the applier, so
        /// reordering <see cref="NexDragVisual"/> cannot silently repoint authored screens.
        /// </remarks>
        private static void ApplyDragVisual(NXInteractionDragRelay relay, in NexNodeProgram node)
        {
            if (node.TryGetProperty("drag.visual", out var visual) &&
                !string.IsNullOrEmpty(visual.Text) &&
                Enum.TryParse<NexDragVisual>(visual.Text, true, out var parsed))
            {
                relay.Visual = parsed;
            }

            if (node.TryGetProperty("drag.ghostOpacity", out var opacity))
                relay.GhostOpacity = Mathf.Clamp01(opacity.Number);

            if (node.TryGetProperty("drag.returnOnFail", out var restore))
                relay.ReturnOnFailedDrop = restore.Flag;
        }

        /// <summary>The same for the drag relay, which is a separate component.</summary>
        private sealed class DragRelayTeardown : IDisposable
        {
            private NXInteractionDragRelay _relay;

            public DragRelayTeardown(NXInteractionDragRelay relay) => _relay = relay;

            public void Dispose()
            {
                if (_relay != null)
                {
                    _relay.enabled = false;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(_relay);
                    else UnityEngine.Object.DestroyImmediate(_relay);
                }
                _relay = null;
            }
        }

        private static Transform FindCanvas()
        {
#if UNITY_2023_1_OR_NEWER
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
#else
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
#endif
            return canvas != null ? canvas.transform : null;
        }

        /// <summary>Removes the click listener on teardown even if the button outlives the screen.</summary>
        private sealed class ClickUnsubscribe : IDisposable
        {
            private Button _button;

            public ClickUnsubscribe(Button button) => _button = button;

            public void Dispose()
            {
                if (_button != null) _button.onClick.RemoveAllListeners();
                _button = null;
            }
        }
    }
}
