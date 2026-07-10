using System;

namespace emiteat.NexUI.Prompt
{
    /// <summary>
    /// Backend/input-package-agnostic holder for "which physical device was used most
    /// recently" (B6: device-specific icon swapping). This assembly has no dependency on
    /// Unity.InputSystem - <c>Integrations.InputSystem</c>'s device tracker calls
    /// <see cref="SetCurrent"/> when it detects a device change; anything that renders a prompt
    /// glyph (via <see cref="UIPromptGlyphTable"/>) reads <see cref="Current"/> or subscribes to
    /// <see cref="DeviceChanged"/> without needing to reference Unity.InputSystem itself.
    /// </summary>
    public static class UICurrentDeviceService
    {
        public static UIPromptDevice Current { get; private set; } = UIPromptDevice.KeyboardMouse;

        public static event Action<UIPromptDevice> DeviceChanged;

        public static void SetCurrent(UIPromptDevice device)
        {
            if (Current == device) return;
            Current = device;
            DeviceChanged?.Invoke(device);
        }
    }
}
