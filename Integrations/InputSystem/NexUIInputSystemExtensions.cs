#if NEXUI_HAS_INPUTSYSTEM
using UnityEngine.InputSystem;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Integrations.InputSystem
{
    /// <summary>Convenience wiring for the Input System policy.</summary>
    public static class NexUIInputSystemExtensions
    {
        public static InputSystemPolicy RegisterInputSystem(
            this UIManager manager,
            InputActionAsset actions,
            string gameplayMap = "Gameplay",
            string uiMap = "UI",
            bool trackCurrentDevice = true)
        {
            var switcher = new InputActionMapSwitcher(actions, gameplayMap, uiMap);
            var policy = new InputSystemPolicy(switcher);
            manager.RegisterInputPolicy(policy);

            // B6: device-specific icon swapping - starts feeding UICurrentDeviceService from
            // live device activity. The tracker stays alive via its own event subscription
            // (nothing needs to hold a reference to it), so this is fire-and-forget.
            if (trackCurrentDevice)
                _ = new UICurrentDeviceTracker();

            return policy;
        }
    }
}
#endif
