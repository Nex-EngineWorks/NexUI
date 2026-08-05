using System.Collections.Generic;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Scenario;
using emiteat.NexUI.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Runs a scenario against a live uGUI screen.
    /// </summary>
    /// <remarks>
    /// Clicks go through <c>Button.onClick.Invoke()</c> rather than through the EventSystem. That
    /// is a deliberate limit, not an oversight: invoking the event tests the screen's own wiring -
    /// bindings, interaction rules, command handlers - without also depending on a camera, a
    /// raycaster, a physics setup and a real pointer position. Whether the button is reachable by
    /// an actual pointer is a different question, and a scenario that failed for that reason would
    /// be reporting a scene problem as a UI-logic problem.
    ///
    /// Real pointer injection belongs with the input-device work the feature specification lists
    /// under Scenario Recorder, and would sit behind this same port.
    /// </remarks>
    public sealed class NexUGuiScenarioWorld : INexScenarioWorld
    {
        private readonly NexScreenRuntime _runtime;
        private readonly UIStateStore _store;
        private readonly List<NexDiagnostic> _diagnostics = new List<NexDiagnostic>();

        public IReadOnlyList<NexDiagnostic> Diagnostics => _diagnostics;

        public NexUGuiScenarioWorld(NexScreenRuntime runtime, UIStateStore store,
            NexCommandRouterBridge router = null)
        {
            _runtime = runtime;
            _store = store;
            router?.Attach(_diagnostics);

            // Interaction failures are the screen raising a problem at the author, so a scenario
            // that asked for no errors should see them.
            if (runtime?.Interactions != null)
                runtime.Interactions.DiagnosticRaised += _diagnostics.Add;
        }

        public bool TryFind(string automationId, out int nodeIndex)
        {
            nodeIndex = _runtime?.Program != null ? _runtime.Program.IndexOfAutomationId(automationId) : -1;
            return nodeIndex >= 0;
        }

        public void Click(int nodeIndex)
        {
            var button = Resolve(nodeIndex)?.GetComponent<Button>();
            if (button != null) button.onClick.Invoke();
        }

        public bool IsVisible(int nodeIndex)
        {
            var go = Resolve(nodeIndex);
            return go != null && go.activeInHierarchy;
        }

        public string GetText(int nodeIndex)
        {
            var go = Resolve(nodeIndex);
            if (go == null) return string.Empty;

            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            return label != null ? label.text : string.Empty;
        }

        public bool TryGetState(string key, out object value)
        {
            if (_store != null) return _store.TryGet(key, out value);
            value = null;
            return false;
        }

        public void SetState(string key, object value) => _store?.Set(key, value);

        private GameObject Resolve(int nodeIndex)
            => _runtime?.SourceMap != null ? _runtime.SourceMap.InstanceAt(nodeIndex) as GameObject : null;
    }

    /// <summary>
    /// Lets a scenario collect the diagnostics a command router raises.
    /// </summary>
    /// <remarks>
    /// A tiny separate type because <c>NexCommandRouter</c> is owned by the game, not by the
    /// scenario: the scenario subscribes to it for the duration of a run and must not take
    /// ownership of it or of its other subscribers.
    /// </remarks>
    public sealed class NexCommandRouterBridge
    {
        private readonly Interaction.NexCommandRouter _router;

        public NexCommandRouterBridge(Interaction.NexCommandRouter router) => _router = router;

        internal void Attach(List<NexDiagnostic> sink)
        {
            if (_router != null && sink != null) _router.DiagnosticRaised += sink.Add;
        }
    }
}
