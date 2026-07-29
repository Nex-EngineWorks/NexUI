# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added
- **MotionClip** module (`Runtime/MotionClip/`, new `emiteat.NexUI.MotionClip` asmdef): a
  multi-element, multi-property, keyframe-based animation system (`UIMotionClip`,
  `UIMotionClipTrack`/`PropertyTrack`/`Keyframe`/`Value`, `UIMotionClipEvaluator`,
  `UIMotionClipTargetResolver`, `IUIMotionClipPlayer`/`UIMotionClipPlayer`) parallel to (and
  independent of) the existing step-based `Motion` module. Editor authoring lives in the
  Designer package (`Tools/NexUI/Utilities > Motion Clip Editor`).
- `UIManager.PlayMotionClipAsync` extension method (`UIManagerMotionClipExtensions`) — plays a
  `UIMotionClip` against a currently open screen's surface, without any change to `UIManager`
  itself.
- `IUISizeCapability` (`Runtime/Abstractions/Capabilities.cs`) plus UGUI (`RectTransform.sizeDelta`)
  and UI Toolkit (`style.width`/`height`) implementations — the runtime previously had no way to
  read/write an element's size by id; only the editor-only Designer backend did.
- `Samples~/MotionClipDemo` sample.

### Notes
- Existing `Motion`/`Abstractions`/`Core` behavior is unchanged; all of the above is additive
  (new files/asmdef, new capability interface, new extension method).

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
