# Themes

Author `UITheme` assets here (*Create → NexUI → Theme*). Suggested token keys:

```
color.bg      color.surface  color.primary  color.danger  color.text
space.xs      space.sm       space.md       space.lg
radius.sm     radius.md      radius.lg
motion.fast   motion.normal  motion.slow
```

Register at runtime with `NexUIThemeAPI.RegisterTheme(theme)` and activate with
`NexUIThemeAPI.SetActiveTheme("<themeId>")`. Values are applied per-backend through the
registered `IUIThemeApplier` (color tokens map to element color; radius tokens to corner
radius in the UI Toolkit applier).
