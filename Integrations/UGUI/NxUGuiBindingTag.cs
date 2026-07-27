using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    public enum NexUIElementOwnership
    {
        Unknown = 0,
        UserOwned = 1,
        DesignerOwned = 2
    }

    /// <summary>
    /// Marks a uGUI GameObject with a stable NexUI element id so a surface can resolve it
    /// by <c>TryFind(id)</c> without relying on GameObject names. Attach in prefabs to any
    /// element that bindings / motion / focus need to target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NxUGuiBindingTag : MonoBehaviour
    {
        [Tooltip("Immutable Designer identity used to match metadata across element renames.")]
        public string stableId;

        [Tooltip("Public id used by NexUI surfaces, bindings and focus targeting.")]
        public string elementId;

        [Tooltip("Tracks whether NexUI Designer created this GameObject. User-owned objects are never deleted by synchronization.")]
        public NexUIElementOwnership ownership;

        public string ResolveId => string.IsNullOrEmpty(elementId) ? name : elementId;
    }
}
