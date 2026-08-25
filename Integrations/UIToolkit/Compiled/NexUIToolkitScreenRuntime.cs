using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.Motion;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// One live instance of a compiled screen built as a UI Toolkit element tree.
    /// </summary>
    /// <remarks>
    /// The uGUI counterpart's twin, and deliberately so: the same properties, the same lifetime,
    /// the same disposal contract. A caller that has driven one compiled screen should not have to
    /// learn a second API to drive the other, because the whole point of the compiled program is
    /// that the author never chose a backend.
    ///
    /// What differs is only what a live object <em>is</em>. A <see cref="VisualElement"/> is not a
    /// <c>UnityEngine.Object</c>, so it is not destroyed - it is removed from its parent, and the
    /// managed tree under it becomes garbage like any other object graph. That is why disposal here
    /// unhooks callbacks explicitly rather than relying on the object dying.
    /// </remarks>
    public sealed class NexUIToolkitScreenRuntime : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly List<CompiledMotionBinding> _motions = new List<CompiledMotionBinding>();
        private bool _disposed;

        public NexScreenProgram Program { get; }

        /// <summary>Root element of the built tree. Detached by <see cref="Dispose"/>.</summary>
        public VisualElement Root { get; }

        /// <summary>Compiled node &lt;-&gt; live element, for the runtime debugger and flow trace.</summary>
        public NexRuntimeSourceMap SourceMap { get; }

        /// <summary>Runs this screen's authored rules. Never null; empty when the screen authored none.</summary>
        public Interaction.NexInteractionRuntime Interactions { get; private set; }

        /// <summary>Who last changed each node property. Answers "why does it say that?".</summary>
        public Overrides.NexOverrideLedger Overrides { get; internal set; }

        /// <summary>Resolves this screen's conditional layers - responsive rules and states.</summary>
        public NexUIToolkitConditionApplier Conditions { get; internal set; }

        public string ScreenId => Program != null ? Program.ScreenId : string.Empty;

        public bool IsDisposed => _disposed;

        internal INexScreenSurface Surface;

        /// <summary>
        /// Value and text handles for the controls this screen built, by authoring node id.
        /// </summary>
        /// <remarks>
        /// Kept on the runtime rather than passed along the build loop because in UI Toolkit the
        /// control is created before the node is wired - the element <em>is</em> the control - so
        /// the handle exists a few steps earlier than the binding that needs it. The uGUI builder
        /// has no equivalent because there the control is attached at the moment it is bound.
        /// </remarks>
        internal readonly Dictionary<string, INexValueHandle> ValueHandles =
            new Dictionary<string, INexValueHandle>();

        internal readonly Dictionary<string, INexTextHandle> TextHandles =
            new Dictionary<string, INexTextHandle>();

        internal NexUIToolkitScreenRuntime(NexScreenProgram program, VisualElement root,
            NexRuntimeSourceMap sourceMap)
        {
            Program = program;
            Root = root;
            SourceMap = sourceMap;
        }

        internal void Track(IDisposable subscription)
        {
            if (subscription != null) _subscriptions.Add(subscription);
        }

        internal void AttachMotion(CompiledMotionBinding motion)
        {
            if (motion == null) return;
            _motions.Add(motion);
            Track(motion);
        }

        internal void PlayEntryMotions()
        {
            for (var i = 0; i < _motions.Count; i++) Observe(_motions[i].PlayEntryAsync());
        }

        /// <summary>Plays every authored exit variant; await before disposing the screen.</summary>
        public Task PlayExitMotionsAsync()
        {
            var tasks = new Task[_motions.Count];
            for (var i = 0; i < _motions.Count; i++) tasks[i] = _motions[i].PlayExitAsync();
            return Task.WhenAll(tasks);
        }

        private static async void Observe(Task task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        internal void AttachInteractions(Interaction.NexInteractionRuntime interactions)
            => Interactions = interactions;

        /// <summary>
        /// Raises <c>OnShow</c> for the screen. Called by the builder once the whole tree exists,
        /// never per node - a rule that hides a sibling must not run while that sibling is still
        /// being built.
        /// </summary>
        public void RaiseShow() => Interactions?.FireAll(NexTrigger.OnShow);

        /// <summary>Sets a node's text from game code and records that game code did it.</summary>
        public void SetText(string authoringNodeId, string text, string reason = null)
        {
            var index = Program != null ? Program.IndexOfNode(authoringNodeId) : -1;
            if (index < 0) return;

            Surface?.SetText(index, text);
            Overrides?.Record(index, emiteat.NexUI.Overrides.NexOverrideProperty.Text,
                emiteat.NexUI.Overrides.NexOverrideSource.GameCode, text, reason);
        }

        /// <summary>Shows or hides a node from game code, recording that game code did it.</summary>
        public void SetVisible(string authoringNodeId, bool visible, string reason = null)
        {
            var index = Program != null ? Program.IndexOfNode(authoringNodeId) : -1;
            if (index < 0) return;

            Surface?.SetVisible(index, visible);
            Overrides?.Record(index, emiteat.NexUI.Overrides.NexOverrideProperty.Visible,
                emiteat.NexUI.Overrides.NexOverrideSource.GameCode, visible ? "true" : "false", reason);
        }

        /// <summary>Why a node property holds its current value. See <see cref="Overrides"/>.</summary>
        public string Explain(string authoringNodeId, emiteat.NexUI.Overrides.NexOverrideProperty property)
        {
            var index = Program != null ? Program.IndexOfNode(authoringNodeId) : -1;
            return index >= 0 && Overrides != null ? Overrides.Explain(index, property) : string.Empty;
        }

        /// <summary>The live element built for an authoring element, or null.</summary>
        public VisualElement Find(string authoringNodeId)
            => SourceMap != null ? SourceMap.InstanceOfNode(authoringNodeId) as VisualElement : null;

        /// <summary>The live element an automated test asked for by automation id, or null.</summary>
        public VisualElement FindByAutomationId(string automationId)
        {
            var index = Program != null ? Program.IndexOfAutomationId(automationId) : -1;
            return index >= 0 && SourceMap != null ? SourceMap.InstanceAt(index) as VisualElement : null;
        }

        /// <summary>Every live element with a given semantic role, in compiled node order.</summary>
        public IEnumerable<VisualElement> FindByRole(Accessibility.AccessibilityRole role)
        {
            if (Program == null || SourceMap == null) yield break;

            var nodes = Program.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Role != role) continue;
                if (SourceMap.InstanceAt(i) is VisualElement element) yield return element;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                try { _subscriptions[i]?.Dispose(); }
                catch (Exception) { /* one bad teardown must not strand the rest */ }
            }
            _subscriptions.Clear();

            Interactions = null;

            // Detached rather than destroyed: a VisualElement is managed, so removing the only
            // reference the panel holds is what ends its life.
            Root?.RemoveFromHierarchy();
        }
    }
}
