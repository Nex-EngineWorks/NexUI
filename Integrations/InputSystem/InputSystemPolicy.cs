#if NEXUI_HAS_INPUTSYSTEM
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Integrations.InputSystem
{
    /// <summary>
    /// Switches Input System action maps when NexUI screens open / close: while any
    /// input-trapping screen (modal or a screen that blocks input) is open, the Gameplay
    /// map is disabled and the UI map enabled; restored via reference counting on close.
    /// </summary>
    public sealed class InputSystemPolicy : IInputPolicy
    {
        private readonly InputActionMapSwitcher _switcher;
        private int _depth;

        public InputSystemPolicy(InputActionMapSwitcher switcher) => _switcher = switcher;

        public void Apply(UIScreenDefinition definition)
        {
            if (!ShouldTrap(definition)) return;
            if (_depth++ == 0)
                _switcher?.ToUI();
        }

        public void Release(UIScreenDefinition definition)
        {
            if (!ShouldTrap(definition)) return;
            if (_depth > 0 && --_depth == 0)
                _switcher?.ToGameplay();
        }

        private static bool ShouldTrap(UIScreenDefinition def)
            => def != null &&
               (def.policy.blockInputBehind || def.layer.layerType == UILayerType.Modal);
    }
}
#endif
