using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Motion;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// One live instance of a compiled screen: the objects that were built, the source map that
    /// ties them back to the authoring document, and the subscriptions that keep them updated.
    /// </summary>
    /// <remarks>
    /// Owning the subscriptions here rather than on the individual GameObjects is what makes
    /// teardown reliable. A binder attached to a destroyed object is a leak that only shows up
    /// as a slow drift in a long session; disposing them from one place means closing a screen
    /// is a single deterministic operation with nothing left watching the state store.
    /// </remarks>
    public sealed class NexScreenRuntime : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly List<CompiledMotionBinding> _motions = new List<CompiledMotionBinding>();
        private bool _disposed;

        public NexScreenProgram Program { get; }

        /// <summary>
        /// Runs this screen's authored rules. Never null; empty when the screen authored none.
        /// </summary>
        public Interaction.NexInteractionRuntime Interactions { get; private set; }

        /// <summary>
        /// Who last changed each node property. Answers "why does it say that?".
        /// </summary>
        public Overrides.NexOverrideLedger Overrides { get; internal set; }

        /// <summary>
        /// Resolves this screen's conditional layers - responsive rules and states. Never null;
        /// empty when neither was authored.
        /// </summary>
        /// <remarks>
        /// Exposed rather than hidden behind a <c>SetState</c> method on the runtime, because the
        /// caller usually wants to ask what exists before choosing - a slot that shows Locked only
        /// when the game says so still has to know Locked is a state this screen has - and because
        /// telling the screen its viewport is a second thing the caller has to be able to do.
        /// </remarks>
        public NexUGuiConditionApplier Conditions { get; internal set; }

        /// <summary>
        /// Sets a node's text from game code and records that game code did it.
        /// </summary>
        /// <remarks>
        /// The supported way for a project to poke at a compiled screen. Writing to the
        /// <c>TextMeshProUGUI</c> directly still works and still shows on screen, but nothing
        /// records it, and the author is then back to guessing why the label disagrees with the
        /// document. <paramref name="reason"/> is what the debugger shows them instead.
        /// </remarks>
        public void SetText(string authoringNodeId, string text, string reason = null)
        {
            var index = Program != null ? Program.IndexOfNode(authoringNodeId) : -1;
            if (index < 0) return;

            _surface?.SetText(index, text);
            Overrides?.Record(index, emiteat.NexUI.Overrides.NexOverrideProperty.Text,
                emiteat.NexUI.Overrides.NexOverrideSource.GameCode, text, reason);
        }

        /// <summary>Shows or hides a node from game code, recording that game code did it.</summary>
        public void SetVisible(string authoringNodeId, bool visible, string reason = null)
        {
            var index = Program != null ? Program.IndexOfNode(authoringNodeId) : -1;
            if (index < 0) return;

            _surface?.SetVisible(index, visible);
            Overrides?.Record(index, emiteat.NexUI.Overrides.NexOverrideProperty.Visible,
                emiteat.NexUI.Overrides.NexOverrideSource.GameCode, visible ? "true" : "false", reason);
        }

        /// <summary>Why a node property holds its current value. See <see cref="Overrides"/>.</summary>
        public string Explain(string authoringNodeId, emiteat.NexUI.Overrides.NexOverrideProperty property)
        {
            var index = Program != null ? Program.IndexOfNode(authoringNodeId) : -1;
            return index >= 0 && Overrides != null ? Overrides.Explain(index, property) : string.Empty;
        }

        internal Interaction.INexScreenSurface _surface;

        /// <summary>Root object of the built hierarchy. Destroyed by <see cref="Dispose"/>.</summary>
        public GameObject Root { get; }

        /// <summary>Compiled node &lt;-&gt; live object, for the runtime debugger and flow trace.</summary>
        public NexRuntimeSourceMap SourceMap { get; }

        public string ScreenId => Program != null ? Program.ScreenId : string.Empty;

        public bool IsDisposed => _disposed;

        internal NexScreenRuntime(NexScreenProgram program, GameObject root, NexRuntimeSourceMap sourceMap)
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
            catch (Exception ex) { Debug.LogException(ex); }
        }

        internal void AttachInteractions(Interaction.NexInteractionRuntime interactions)
            => Interactions = interactions;

        /// <summary>
        /// Raises <c>OnShow</c> for the screen. Called by the builder once the whole hierarchy
        /// exists, never per node - a rule that hides a sibling must not run while that sibling is
        /// still being built.
        /// </summary>
        public void RaiseShow() => Interactions?.FireAll(Compiled.NexTrigger.OnShow);

        /// <summary>The live GameObject built for an authoring element, or null.</summary>
        public GameObject Find(string authoringNodeId)
            => SourceMap != null ? SourceMap.InstanceOfNode(authoringNodeId) as GameObject : null;

        /// <summary>
        /// The live GameObject an automated test asked for by automation id, or null.
        /// </summary>
        /// <remarks>
        /// The entry point the whole automation-id feature exists for. A test written against this
        /// keeps working when the element is renamed, re-parented, restyled or rebuilt, because the
        /// only thing it named is the promise the author made about what this element <em>is</em>.
        /// </remarks>
        public GameObject FindByAutomationId(string automationId)
        {
            var index = Program != null ? Program.IndexOfAutomationId(automationId) : -1;
            return index >= 0 && SourceMap != null ? SourceMap.InstanceAt(index) as GameObject : null;
        }

        /// <summary>
        /// Every live object with a given semantic role, in compiled node order.
        /// </summary>
        /// <remarks>
        /// Deterministic order matters here: a test that takes "the third list item" must get the
        /// same one on every run, and compiled node order is the document's own top-down order.
        /// </remarks>
        public IEnumerable<GameObject> FindByRole(Accessibility.AccessibilityRole role)
        {
            if (Program == null || SourceMap == null) yield break;

            var nodes = Program.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Role != role) continue;
                if (SourceMap.InstanceAt(i) is GameObject go) yield return go;
            }
        }

        /// <summary>Automation ids this screen exposes, for a test-side sanity check or a report.</summary>
        public IEnumerable<string> AutomationIds
        {
            get
            {
                if (Program == null) yield break;

                var nodes = Program.Nodes;
                for (int i = 0; i < nodes.Length; i++)
                    if (!string.IsNullOrEmpty(nodes[i].AutomationId)) yield return nodes[i].AutomationId;
            }
        }

        /// <summary>
        /// The authoring path of a live object - what a message about this object should call it.
        /// </summary>
        public string AuthoringPathOf(GameObject instance)
            => SourceMap != null ? SourceMap.AuthoringPathOf(instance) : string.Empty;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // OnHide runs while the objects still exist, so a rule can observe or change the
            // screen on the way out. A rule that throws here must not block teardown.
            try { Interactions?.FireAll(Compiled.NexTrigger.OnHide); }
            catch (Exception ex) { Debug.LogException(ex); }

            // Anything still parked mid-sequence is dropped. A delayed action resuming against a
            // destroyed hierarchy is the worst failure this feature can produce: it happens after
            // the user navigated away, and looks like a bug in whatever screen they are on now.
            Interactions?.CancelPending();

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                // A binder that throws while unsubscribing must not strand the remaining ones.
                try { _subscriptions[i].Dispose(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            _subscriptions.Clear();

            SourceMap?.Clear();

            if (Root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
