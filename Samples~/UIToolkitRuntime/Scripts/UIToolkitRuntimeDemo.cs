using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.State;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Samples.UIToolkitRuntime
{
    /// <summary>
    /// UI Toolkit runtime demo: opens the HUD, binds state, and toggles a theme.
    /// Requires a UIDocument + UIToolkitIntegrationBootstrap in the scene and screen
    /// definitions whose backend asset is a VisualTreeAsset.
    /// </summary>
    public sealed class UIToolkitRuntimeDemo : MonoBehaviour
    {
        [SerializeField] private UIScreenDefinition _hud;
        [SerializeField] private UIScreenDefinition _inventory;
        [SerializeField] private UIScreenDefinition _pauseMenu;
        [SerializeField] private UITheme _dark;
        [SerializeField] private UITheme _light;

        private readonly UIStateStore _store = new UIStateStore();

        private async void Start()
        {
            foreach (var def in new[] { _hud, _inventory, _pauseMenu })
                if (def != null) Core.NexUI.RegisterScreen(def);

            if (_dark != null) NexUITheme.Registry.Register(_dark);
            if (_light != null) NexUITheme.Registry.Register(_light);
            NexUITheme.Use(_dark != null ? _dark.themeId : "default");

            _store.Set("player.name", "Hero");
            _store.Set("player.hp", 1f);

            if (_hud != null)
            {
                await Core.NexUI.OpenAsync("HUD");
                BindHud();
            }
        }

        private void BindHud()
        {
            var surface = Core.NexUI.Manager.GetSurface("HUD");
            if (surface == null) return;
            var name = surface.TryFind("nameLabel");
            if (name != null) new UITextBinder().Bind(name, "player.name", _store);
            var hp = surface.TryFind("hpBar");
            if (hp != null) new UIValueBinder().Bind(hp, "player.hp", _store);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I)) Core.NexUI.Toggle("Inventory");
            if (Input.GetKeyDown(KeyCode.Escape)) Core.NexUI.Open("PauseMenu");
            if (Input.GetKeyDown(KeyCode.Backspace)) Core.NexUI.Back();
            if (Input.GetKeyDown(KeyCode.T))
                NexUITheme.Use(NexUITheme.Active != null && NexUITheme.Active.themeId == "dark" ? "light" : "dark");
        }
    }
}
