# Project Setup

`Tools > NexUI > Project Setup` bootstraps everything a fresh project needs. Run it once after
installing the packages; it is idempotent - running again only fills gaps.

## What it creates

| Item | Where |
|---|---|
| Settings assets (runtime, theme, input, integration defaults) | `Assets/NexUI/Settings` |
| Screen registry, icon registry, template registry | `Assets/NexUI/Registries` |
| Starter themes (light/dark token sets) | `Assets/NexUI/Themes` |
| Starter motion presets (fade/slide/scale) | `Assets/NexUI/Motion` |
| Example screen definitions (HUD, Pause, Settings) | `Assets/NexUI/Screens` |
| Default folders for Studio output (UXML/USS/prefabs) | per Project Setup options |
| Scene bootstrap objects (UIDocument+Bootstrap / Canvas+Bootstrap) | optional scene action |

## What it verifies (Setup Doctor)

- Required define symbols (`DOTWEEN` when DOTween is present).
- Assembly references resolve (no missing asmdef references after renames).
- Every registered screen has a factory-capable backend in the current scene set.
- Writable output paths (Studio save targets exist and are not read-only).

## Recommended folder layout

```
Assets/NexUI/
  Screens/     UIScreenDefinition + DesignerMetadata pairs
  Themes/      UITheme assets
  Motion/      UIMotionPreset / UIMotionClip assets
  Registries/  shared registries referenced by settings
```

Screens live as **pairs**: `Foo.Screen.asset` (backend + policy) and
`Foo.Metadata.asset` (elements/bindings/motion authored in Studio). The toolbar's Metadata
"New" button creates the pair for you.

## After setup

1. Open Studio: `Tools > Nex/NexUI Studio > Open NexUI Studio`.
2. Pick a screen in the new switcher dropdown (▾).
3. Drag components from the Library; bind state keys; press Save.
4. In game code: `await NexUIApp.OpenAsync("HUD");`

## Multiple windows

`Tools > Nex/NexUI Studio > Open NexUI Studio` again (or dock a second one) opens an
**independent editing session**: each window owns its own context, its own open screen, undo
history, validation state and autosave snapshot. The satellite tools (AI, Figma, Scenario,
Profiler...) always follow the **focused** window's context. Snapshots and recents are keyed per
document/user, so two windows never fight over each other's files.

