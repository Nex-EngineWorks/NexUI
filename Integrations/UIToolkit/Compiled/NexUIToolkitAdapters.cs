using System.Collections.Generic;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.State;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Lets the interaction engine read and write game state without knowing what a store is.
    /// </summary>
    /// <remarks>
    /// A twin of the uGUI backend's adapter rather than a shared one. The engine lives in
    /// <c>emiteat.NexUI.Interaction</c>, which deliberately does not reference the state assembly -
    /// that is what keeps the rule engine free of any particular state implementation - so the
    /// adapter has to live on the far side of that boundary, which means once per backend. Five
    /// lines each; if this ever grows, the port is being asked to do too much.
    /// </remarks>
    public sealed class NexUIToolkitStateAccess : INexStateAccess
    {
        private readonly UIStateStore _store;

        public NexUIToolkitStateAccess(UIStateStore store) => _store = store;

        public bool TryGet(string key, out object value)
        {
            if (_store != null) return _store.TryGet(key, out value);
            value = null;
            return false;
        }

        public void Set(string key, object value) => _store?.Set(key, value);
    }

    /// <summary>
    /// Lets an interaction action reach the built tree without knowing what a VisualElement is.
    /// </summary>
    /// <remarks>
    /// The uGUI surface's twin over the same <see cref="NexRuntimeSourceMap"/>. That the interaction
    /// runtime needs only these two methods is what let the whole rule engine be shared between the
    /// backends rather than written twice.
    /// </remarks>
    public sealed class NexUIToolkitScreenSurface : INexScreenSurface
    {
        private readonly NexRuntimeSourceMap _sourceMap;

        public NexUIToolkitScreenSurface(NexRuntimeSourceMap sourceMap) => _sourceMap = sourceMap;

        public void SetVisible(int nodeIndex, bool visible)
        {
            var element = Resolve(nodeIndex);
            if (element == null) return;

            // display, not visibility: a hidden element must leave its parent's flex layout too,
            // or hiding one leaves a gap where it was.
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetText(int nodeIndex, string text)
        {
            var element = Resolve(nodeIndex);
            if (element == null) return;

            if (element is TextElement own)
            {
                own.text = text ?? string.Empty;
                return;
            }

            // Reaching into a control's label child, the same way the uGUI surface reaches a
            // Button's text component - the author targeted the element, not its internals.
            var child = element.Q<TextElement>();
            if (child != null) child.text = text ?? string.Empty;
        }

        private VisualElement Resolve(int nodeIndex)
            => _sourceMap != null ? _sourceMap.InstanceAt(nodeIndex) as VisualElement : null;
    }

    /// <summary>
    /// Reports the authored features a compiled screen carries that this backend does not act on.
    /// </summary>
    /// <remarks>
    /// Once per feature per screen, not once per node: a screen where forty slots each declare a
    /// hover motion would otherwise produce forty warnings and bury the sentence they all say.
    ///
    /// The list differs from uGUI's, and that difference is the point. Style classes are native
    /// here and reported there; motion is applied when build options provide a runtime registry.
    /// </remarks>
    public static class NexUIToolkitCarriedFeatureReport
    {
        public static void Report(NexScreenProgram program, NexDiagnosticBag diagnostics,
            bool motionAvailable = false)
        {
            if (program == null || diagnostics == null) return;

            var reported = new HashSet<string>();
            var nodes = program.Nodes;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (!motionAvailable && !nodes[i].Motion.IsEmpty)
                    Once(reported, diagnostics, program.ScreenId, "Motion",
                        "This screen declares motion, but the compiled UI Toolkit runtime has no " +
                        "motion player wired into it. The motion is preserved in the program and " +
                        "starts working once one is.");

                if (!string.IsNullOrEmpty(nodes[i].LocalizationKey))
                    Once(reported, diagnostics, program.ScreenId, "Localization",
                        "This screen links elements to localization keys, but the compiled runtime " +
                        "is not given a localization table to resolve them against, so the authored " +
                        "literal text is shown. The keys are preserved in the program.");

                var style = nodes[i].Style;
                if (style.TokenOverrides != null && style.TokenOverrides.Length > 0)
                    Once(reported, diagnostics, program.ScreenId, "Theme Token Overrides",
                        "This screen overrides theme tokens per element. The compiled runtime " +
                        "resolves no theme, so the overrides are carried but not applied. The style " +
                        "classes beside them are applied - UI Toolkit has a class system.");
            }
        }

        private static void Once(HashSet<string> reported, NexDiagnosticBag diagnostics,
            string screenId, string feature, string detail)
        {
            if (!reported.Add(feature)) return;

            diagnostics.Add(NexDiagnosticCodes.FeatureCarriedNotApplied,
                new NexSourceLocation(screenId, null, null, feature),
                feature + " is carried by the compiled screen but not applied by the UI Toolkit " +
                "backend. " + detail);
        }
    }

    /// <summary>
    /// Applies compiled internal-part nudges to the parts the builder named.
    /// </summary>
    /// <remarks>
    /// UI Toolkit's stock controls build their own internals and expose them by USS class
    /// (<c>.unity-base-slider__dragger</c>), which is what the authoring registry already records
    /// per part as its UI Toolkit selector. So unlike uGUI - where the compiled builder assembles
    /// its own children and has to tag them - the parts here are found by the class Unity itself
    /// put on them, and the mapping from part id to class lives in one table below.
    ///
    /// The table is here rather than compiled into the program for the same reason the uGUI path
    /// carries part ids rather than paths: the selector is this backend's private business, and
    /// baking it into the asset would freeze a Unity implementation detail into published content.
    /// </remarks>
    public static class NexUIToolkitPartApplier
    {
        private static readonly Dictionary<string, string> SelectorByPart =
            new Dictionary<string, string>
            {
                { "track", "unity-base-slider__tracker" },
                { "handle", "unity-base-slider__dragger" },
                { "fill", "unity-progress-bar__progress" },
                { "label", "unity-base-field__label" },
                { "text", "unity-text-element" },
                { "background", "unity-toggle__input" },
                { "checkmark", "unity-toggle__checkmark" },
                { "viewport", "unity-scroll-view__content-viewport" },
                { "content", "unity-scroll-view__content-container" },
                { "vertical-scrollbar", "unity-scroll-view__vertical-scroller" },
                { "horizontal-scrollbar", "unity-scroll-view__horizontal-scroller" }
            };

        public static void Apply(NexScreenProgram program, VisualElement[] built,
            NexDiagnosticBag diagnostics)
        {
            var parts = program?.Parts;
            if (parts == null || parts.IsEmpty || built == null) return;

            var reported = new HashSet<string>();

            for (int i = 0; i < parts.Overrides.Count; i++)
            {
                var over = parts.Overrides[i];
                if (over.NodeIndex < 0 || over.NodeIndex >= built.Length) continue;

                var root = built[over.NodeIndex];
                if (root == null) continue;

                var target = Resolve(root, over.PartId);
                if (target == null)
                {
                    Report(reported, diagnostics, program.ScreenId, over.PartId);
                    continue;
                }

                // Deltas from where the control put the part, so the control's own layout can
                // change without rewriting every authored screen.
                if (over.HasPosition)
                {
                    target.style.left = target.resolvedStyle.left + over.Position.x;
                    target.style.top = target.resolvedStyle.top + over.Position.y;
                }
                if (over.HasSizeDelta)
                {
                    target.style.width = target.resolvedStyle.width + over.SizeDelta.x;
                    target.style.height = target.resolvedStyle.height + over.SizeDelta.y;
                }
                if (over.HasRotation)
                    target.style.rotate = new Rotate(
                        new UnityEngine.UIElements.Angle(over.Rotation, AngleUnit.Degree));
                if (over.HasScale)
                    target.style.scale = new Scale(
                        new UnityEngine.Vector3(over.Scale.x, over.Scale.y, 1f));
                if (over.HasVisibility)
                    target.style.display = over.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static VisualElement Resolve(VisualElement root, string partId)
        {
            if (string.IsNullOrEmpty(partId)) return null;
            if (!SelectorByPart.TryGetValue(partId, out var className)) return null;

            return root.Q(className: className);
        }

        private static void Report(HashSet<string> reported, NexDiagnosticBag diagnostics,
            string screenId, string partId)
        {
            if (diagnostics == null || !reported.Add(partId ?? string.Empty)) return;

            diagnostics.Add(NexDiagnosticCodes.PartNotBuilt,
                new NexSourceLocation(screenId, null, null, partId),
                "A nudge targets the '" + partId + "' part, which this element does not expose on " +
                "the UI Toolkit backend. The nudge is in the program and applies to any backend " +
                "that does build that part.");
        }
    }
}
