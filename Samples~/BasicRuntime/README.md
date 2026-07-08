# Basic Runtime Sample

Demonstrates the NexUI runtime end-to-end: screen open/close/toggle/back, state + bindings,
and motion — on either backend.

## Setup

1. Create a scene with a backend bootstrap:
   - **uGUI:** a `Canvas` (+ `GraphicRaycaster`) and an `EventSystem`; add
     `UGUIIntegrationBootstrap`.
   - **UI Toolkit:** a `UIDocument` (with `PanelSettings`); add `UIToolkitIntegrationBootstrap`.
2. Add an empty GameObject and attach:
   - `BasicRuntimeBootstrap` — set **Backend** to match, then assign your **HUD /
     Inventory / PauseMenu** assets (uGUI prefabs or UI Toolkit `VisualTreeAsset`s).
   - `BasicRuntimeInput` — keyboard controls.
   - `BasicRuntimeStateDemo` — binds `nameLabel` + `hpBar` on the HUD.
3. In your HUD asset, provide elements with ids `nameLabel` (a text) and `hpBar`
   (a Slider for uGUI / ProgressBar for UI Toolkit). For uGUI, add `NxUGuiBindingTag`
   with those ids; for UI Toolkit, use the element `name`.

## Controls

| Key | Action |
|-----|--------|
| `I` | Toggle Inventory |
| `Esc` | Open PauseMenu (modal, pushed on back stack) |
| `Backspace` | Back (closes top modal / pops back stack) |

## What it shows

- **HUD open** on start.
- **Inventory toggle** with a popup (scale + fade) motion.
- **PauseMenu** open as a modal and **Back** to close it.
- **State → binding**: HP drains over time and the bound bar updates live; the name label
  updates from the store.
- **Motion**: presets are built in code in `BasicRuntimeBootstrap` (so the sample runs with
  no pre-authored `.asset` files); assign your own `UIMotionPreset` assets to override.

> Screen definitions and motion presets are created at runtime via
> `ScriptableObject.CreateInstance` for portability. In a real project, author them as
> assets (*Create → NexUI → Screen Definition* / *Motion Preset*).
