# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [0.1.0] - 2026-07-08

### Added
- Initial runtime foundation for NexUI.
- **Abstractions:** `IUISurface`, `IUIElementHandle`, capability interfaces, command
  pipeline contracts, motion timeline data, resource/theme/motion abstractions.
- **Core:** `UIManager`, screen definitions & registry, layer manager, back/modal
  stacks, toast queue, focus manager, policy runner, command dispatcher, `NexUI` facade,
  and a runtime validation suite.
- **State:** observable `UIStateStore`, `UISignal`, derived state, action resolver, and
  capability-based binders (text, value, visibility, class, command).
- **Motion:** authoring assets (preset/variant/step/graph), `MotionCompiler`, and the
  `BuiltInMotionPlayer` fallback (opacity / position / scale / rotation; linear & ease-in-out).
- **Theme:** tokens, themes, registry, runtime overrides, responsive rules, and the
  `NexUIThemeAPI` facade.
- **Components:** backend-agnostic component contracts and a `ComponentRegistry`.
- **Query (optional):** query state, cache, retry, invalidation, and UI boundaries.
- **Integrations:** minimal UI Toolkit and uGUI backends (element handles, surfaces,
  screen factories, focus adapters, theme appliers, sample components, bootstrappers).
- Basic Runtime sample and documentation.

### Notes
- Designer / EditorWindow tooling is intentionally not included in this release.
