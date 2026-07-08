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
            string uiMap = "UI")
        {
            var switcher = new InputActionMapSwitcher(actions, gameplayMap, uiMap);
            var policy = new InputSystemPolicy(switcher);
            manager.RegisterInputPolicy(policy);
            return policy;
        }
    }
}
#endif
