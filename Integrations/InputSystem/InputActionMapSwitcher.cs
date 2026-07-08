#if NEXUI_HAS_INPUTSYSTEM
using UnityEngine.InputSystem;

namespace emiteat.NexUI.Integrations.InputSystem
{
    /// <summary>
    /// Enables/disables Gameplay and UI action maps on an <see cref="InputActionAsset"/>.
    /// </summary>
    public sealed class InputActionMapSwitcher
    {
        private readonly InputActionMap _gameplay;
        private readonly InputActionMap _ui;

        public InputActionMapSwitcher(InputActionAsset asset, string gameplayMap = "Gameplay", string uiMap = "UI")
        {
            if (asset != null)
            {
                _gameplay = asset.FindActionMap(gameplayMap, throwIfNotFound: false);
                _ui = asset.FindActionMap(uiMap, throwIfNotFound: false);
            }
        }

        public InputActionMapSwitcher(InputActionMap gameplayMap, InputActionMap uiMap)
        {
            _gameplay = gameplayMap;
            _ui = uiMap;
        }

        public void ToUI()
        {
            _gameplay?.Disable();
            _ui?.Enable();
        }

        public void ToGameplay()
        {
            _ui?.Disable();
            _gameplay?.Enable();
        }
    }
}
#endif
