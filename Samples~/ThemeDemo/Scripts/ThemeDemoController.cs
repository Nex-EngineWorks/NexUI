using UnityEngine;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Samples.ThemeDemo
{
    public sealed class ThemeDemoController : MonoBehaviour
    {
        [SerializeField] private UITheme _light;
        [SerializeField] private UITheme _dark;

        private readonly ResponsiveRuleSet _responsiveRules = new ResponsiveRuleSet();

        private void Start()
        {
            if (_light != null) NexUITheme.Registry.Register(_light);
            if (_dark != null) NexUITheme.Registry.Register(_dark);
            if (_dark != null) NexUITheme.Use(_dark.themeId);

            _responsiveRules.Add(new ResponsiveRule
            {
                name = "wide",
                minWidth = 1280f,
                overrides = new[] { new ThemeToken { key = "space.md", value = "20" } }
            });
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
                ToggleTheme();

            if (Input.GetKeyDown(KeyCode.R))
                _responsiveRules.Apply(Screen.width, NexUIThemeAPI.Overrides);

            if (Input.GetKeyDown(KeyCode.C))
                NexUIThemeAPI.Overrides.ClearOverrides();
        }

        private void ToggleTheme()
        {
            if (_light == null || _dark == null) return;
            NexUITheme.Use(NexUITheme.Active == _dark ? _light.themeId : _dark.themeId);
        }
    }
}
