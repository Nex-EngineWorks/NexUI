# NexUI

A backend-independent **runtime UI framework** for Unity. NexUI separates *what* your UI
does (screens, state, bindings, commands, motion, theme) from *how* it is rendered
(UI Toolkit or uGUI), so the same game-side code drives either backend.

> This package is the **Runtime** foundation. A visual **Designer** editor extension is
> intentionally out of scope here, but the runtime is structured so a Designer can be added
> later without changing the public API.

## Architecture

```
Abstractions   (interfaces + capabilities + compiled motion timeline — no backend types)
    ^
    ├── Core        (UIManager, screens, layers, stacks, command pipeline, validation)
    ├── State       (state store, signals, capability-based binders)
    ├── Motion      (authoring assets, compiler, built-in player)
    ├── MotionClip  (multi-element keyframe timeline clips + player, parallel to Motion)
    ├── Theme       (tokens, registry, appliers)
    └── Components   (backend-agnostic component contracts)

Query  → Abstractions, State   (optional; compiles without Core)

Integrations.UIToolkit → all runtime modules
Integrations.UGUI      → all runtime modules
```

### Golden rules

- **Core never references** `VisualElement`, `GameObject`, `RectTransform`, `Canvas`, UI
  Toolkit or uGUI. It only knows `IUISurface`, `IUIElementHandle` and capabilities.
- **Motion never references** UI Toolkit, uGUI or DOTween. It animates through
  `IUITransformCapability` only.
- **Query never references** Core.
- Backends live **only** in `Integrations.*`.

## Dependencies

- Unity **6000.4+**. Async APIs use **UniTask** (`com.cysharp.unitask`) throughout.
- uGUI integration references `com.unity.ugui` and `Unity.TextMeshPro`.

## Quick start

1. Add a backend bootstrap to your scene:
   - **UI Toolkit:** add `UIToolkitIntegrationBootstrap` next to a `UIDocument`.
   - **uGUI:** add `UGUIIntegrationBootstrap` next to a `Canvas`.
2. Create `UIScreenDefinition` assets (menu: *Create → NexUI → Screen Definition*).
3. Register and open:

```csharp
NexUI.RegisterScreen(myScreenDefinition);
await NexUI.OpenAsync("HUD");
```

See `Samples~/BasicRuntime`, `Samples~/UIToolkitRuntime`, `Samples~/UGUIRuntime`,
and `Documentation~/how-to-use.md`.

## Optional Integrations

DOTween, VContainer, MessagePipe, Addressables, and Input System support live in
`Integrations/*`. Each module is guarded by an optional define so the base runtime
continues to compile when the dependency is absent. See `Documentation~/integrations.md`.

## Project Setup

Use `Tools/NexUI/Project Setup` to create settings, registries, default folders, starter
themes, starter motions, and starter screen definitions. See `Documentation~/project-setup.md`.

## Runtime Debug Overlay

Call `NexUIDebug.ShowOverlay()` or open the editor debug tools to inspect opened screens,
layers, stacks, state keys, command log, query cache, and theme state at runtime.

## Validation

Use `Tools/NexUI/Validator` to scan registries and scene setup for duplicate IDs, missing
assets, backend mismatches, motion/theme issues, and command binding problems. See
`Documentation~/validation.md`.

## Command Pipeline

Commands can be logged, replayed, and inverted by `IUndoableCommand`. See
`Documentation~/command-pipeline.md`.

## Samples

- Basic Runtime
- UI Toolkit Runtime
- uGUI Runtime
- Integration Demo
- Motion Demo
- Motion Clip Demo
- Theme Demo
