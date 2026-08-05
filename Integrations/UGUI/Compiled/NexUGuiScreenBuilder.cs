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
                WireInteractionTriggers(runtime, interactions, i, rect);

                rect.gameObject.SetActive(node.Visible);
            }

            // Only screens that actually park a rule mid-sequence get a per-frame pump.
            if (!interactions.IsEmpty && program.Interactions.HasDelays())
                NexScreenTicker.Attach(root, interactions);

            // Only once the whole hierarchy exists: an OnShow rule that hides or retargets another
            // element must not run while that element is still being constructed.
            runtime.RaiseShow();

            return runtime;
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

            var text = rect.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null) return;

            var key = node.TextBindingKey;
            runtime.Track(options.Store.Watch<object>(key, value =>
            {
                var next = value != null ? value.ToString() : string.Empty;

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
        private static void WireInteractionTriggers(NexScreenRuntime runtime,
            NexInteractionRuntime interactions, int nodeIndex, RectTransform rect)
        {
            if (!interactions.WantsClickListener(nodeIndex)) return;

            var button = rect.GetComponent<Button>();
            if (button == null) return; // The compiler already reported this as NEX-BND-4004.

            button.onClick.AddListener(() => interactions.Fire(nodeIndex, NexTrigger.OnClick));
            runtime.Track(new ClickUnsubscribe(button));
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
