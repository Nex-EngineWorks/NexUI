#if NEXUI_HAS_INPUTSYSTEM
using emiteat.NexUI.Prompt;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace emiteat.NexUI.Integrations.InputSystem
{
    /// <summary>
    /// Feeds <see cref="UICurrentDeviceService"/> from Input System device activity, so prompt
    /// glyphs (B6: device-specific icon swapping) follow whichever device the player last used.
    /// Classifies gamepads by display-name heuristics since the Input System has no first-class
    /// controller-brand concept.
    /// </summary>
    public sealed class UICurrentDeviceTracker : System.IDisposable
    {
        public UICurrentDeviceTracker() => UnityEngine.InputSystem.InputSystem.onEvent += OnEvent;

        public void Dispose() => UnityEngine.InputSystem.InputSystem.onEvent -= OnEvent;

        private void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device == null || !eventPtr.valid) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            var classified = Classify(device);
            if (classified.HasValue)
                UICurrentDeviceService.SetCurrent(classified.Value);
        }

        private static UIPromptDevice? Classify(InputDevice device)
        {
            if (device is Gamepad gamepad) return ClassifyGamepad(gamepad);
            if (device is Keyboard || device is Mouse) return UIPromptDevice.KeyboardMouse;
            return null;
        }

        private static UIPromptDevice ClassifyGamepad(Gamepad gamepad)
        {
            var name = (gamepad.displayName + " " + gamepad.description.product).ToLowerInvariant();
            if (name.Contains("dualsense") || name.Contains("dualshock") || name.Contains("playstation"))
                return UIPromptDevice.PlayStation;
            if (name.Contains("pro controller") || name.Contains("switch") || name.Contains("joy-con"))
                return UIPromptDevice.Switch;
            if (name.Contains("steam deck") || name.Contains("steam controller"))
                return UIPromptDevice.SteamDeck;
            return UIPromptDevice.Xbox; // default gamepad bucket when no brand keyword matches
        }
    }
}
#endif
