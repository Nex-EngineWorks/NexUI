using UnityEngine;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Samples.BasicRuntime
{
    /// <summary>
    /// Minimal keyboard driver for the sample using legacy Input (no InputSystem binding,
    /// per the runtime scope). I = toggle Inventory, Esc = open PauseMenu, Backspace = Back.
    /// </summary>
    public sealed class BasicRuntimeInput : MonoBehaviour
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
                Core.NexUI.Toggle("Inventory");

            if (Input.GetKeyDown(KeyCode.Escape))
                Core.NexUI.Open("PauseMenu");

            if (Input.GetKeyDown(KeyCode.Backspace))
                Core.NexUI.Back();
        }
#else
        private void Awake()
        {
            Debug.LogWarning(
                "[NexUI Sample] Legacy Input is disabled. Enable it in Player Settings > " +
                "Active Input Handling (Both/Old), or drive NexUI.Open/Toggle/Back from your own input code.");
        }
#endif
    }
}
