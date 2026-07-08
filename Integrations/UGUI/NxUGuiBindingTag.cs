using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Marks a uGUI GameObject with a stable NexUI element id so a surface can resolve it
    /// by <c>TryFind(id)</c> without relying on GameObject names. Attach in prefabs to any
    /// element that bindings / motion / focus need to target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NxUGuiBindingTag : MonoBehaviour
    {
        [Tooltip("Stable id used by NexUI surfaces, bindings and focus targeting.")]
        public string elementId;

        public string ResolveId => string.IsNullOrEmpty(elementId) ? name : elementId;
    }
}
