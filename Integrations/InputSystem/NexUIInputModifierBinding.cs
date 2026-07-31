using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace emiteat.NexUI.Integrations.InputSystem
{
    /// <summary>
    /// Installs the Input System as the source of multi-selection modifiers for NexUI collections.
    /// </summary>
    /// <remarks>
    /// This assembly only compiles when com.unity.inputsystem is installed (see the asmdef's
    /// versionDefines), so a project without the package keeps the safe default of "no modifiers"
    /// rather than failing to build.
    ///
    /// Installed on load rather than requiring a scene object, because a collection that silently
    /// refuses Ctrl-click until some bootstrap component is added is worse than one that never
    /// supported it.
    /// </remarks>
    public static class NexUIInputModifierBinding
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            // Never replace a probe a project installed deliberately.
            if (NXInputModifierProbe.Provider != null) return;
            NXInputModifierProbe.Provider = Read;
        }

        private static NXInputModifiers Read()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return NXInputModifiers.None;

            var modifiers = NXInputModifiers.None;
            if (keyboard.ctrlKey.isPressed || keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed)
                modifiers |= NXInputModifiers.Additive;
            if (keyboard.shiftKey.isPressed)
                modifiers |= NXInputModifiers.Range;
            return modifiers;
        }
    }
}
