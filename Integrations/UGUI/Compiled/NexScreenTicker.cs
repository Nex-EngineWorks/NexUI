using emiteat.NexUI.Interaction;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Drives a screen's parked interaction rules once per frame.
    /// </summary>
    /// <remarks>
    /// Created by the builder only for screens whose compiled program actually contains a delay,
    /// so a screen with no delayed rule has no <c>Update</c> in the scene at all. A per-frame
    /// callback that exists on every screen "just in case" is exactly the kind of cost the
    /// pay-for-what-you-use rule is meant to keep out.
    ///
    /// It lives on the screen root, so Unity destroying the hierarchy stops the pump without
    /// anyone having to remember to unsubscribe.
    /// </remarks>
    [AddComponentMenu("")]
    internal sealed class NexScreenTicker : MonoBehaviour
    {
        private NexInteractionRuntime _interactions;

        public static void Attach(GameObject root, NexInteractionRuntime interactions)
        {
            if (root == null || interactions == null) return;

            var ticker = root.AddComponent<NexScreenTicker>();
            ticker._interactions = interactions;
            ticker.hideFlags = HideFlags.HideInInspector;
        }

        private void Update() => _interactions?.Tick();
    }
}
