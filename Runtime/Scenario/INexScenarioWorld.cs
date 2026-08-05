using System.Collections.Generic;
using emiteat.NexUI.Diagnostics;

namespace emiteat.NexUI.Scenario
{
    /// <summary>
    /// Everything a scenario needs from the running screen.
    /// </summary>
    /// <remarks>
    /// The same port pattern the interaction engine uses, for the same reason: the runner stays
    /// free of Unity and of any particular backend, so a scenario can be executed against a real
    /// uGUI screen, against a UI Toolkit one, or against a fake in a test that has no GameObjects
    /// at all. That last case is what lets the runner itself be tested.
    ///
    /// Elements are addressed by compiled node index, resolved once by <see cref="TryFind"/>, so
    /// the rest of the interface never repeats a lookup.
    /// </remarks>
    public interface INexScenarioWorld
    {
        /// <summary>Resolves an automation id to a node index. False when the screen has no such element.</summary>
        bool TryFind(string automationId, out int nodeIndex);

        void Click(int nodeIndex);

        bool IsVisible(int nodeIndex);

        /// <summary>The element's displayed text, or empty when it displays none.</summary>
        string GetText(int nodeIndex);

        bool TryGetState(string key, out object value);

        void SetState(string key, object value);

        /// <summary>
        /// Diagnostics the screen raised since the scenario started. Backs
        /// <see cref="NexScenarioStepKind.AssertNoErrors"/>.
        /// </summary>
        IReadOnlyList<NexDiagnostic> Diagnostics { get; }
    }
}
