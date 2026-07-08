using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.State;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Samples.UGUIRuntime
{
    public sealed class UGUIRuntimeDemo : MonoBehaviour
    {
        [SerializeField] private UIScreenDefinition _hud;
        [SerializeField] private UIScreenDefinition _pauseMenu;
        [SerializeField] private UITheme _dark;
        [SerializeField] private UITheme _light;

        private readonly UIStateStore _store = new UIStateStore();
        private float _hp = 1f;

        private async void Start()
        {
            if (_hud != null) NexUI.RegisterScreen(_hud);
            if (_pauseMenu != null) NexUI.RegisterScreen(_pauseMenu);

            if (_dark != null) NexUITheme.Registry.Register(_dark);
            if (_light != null) NexUITheme.Registry.Register(_light);
            if (_dark != null) NexUITheme.Use(_dark.themeId);

            _store.Set("player.hp", _hp);
            _store.Set("player.name", "NexUI Pilot");

            if (_hud != null)
                await NexUI.OpenAsync(_hud.ScreenId);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && _pauseMenu != null)
                NexUI.Toggle(_pauseMenu.ScreenId);

            if (Input.GetKeyDown(KeyCode.T))
                NexUITheme.Use(NexUITheme.Active != null && NexUITheme.Active.themeId == "dark" ? "light" : "dark");

            if (Input.GetKeyDown(KeyCode.H))
            {
                _hp = Mathf.Max(0f, _hp - 0.1f);
                _store.Set("player.hp", _hp);
            }
        }
    }
}
