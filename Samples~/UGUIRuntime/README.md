# NexUI uGUI Runtime Sample

This sample shows the uGUI backend using the same runtime concepts as the UI Toolkit sample:
screen definitions, state binding, button actions, motion, and theme token application.

## Scene Setup

1. Create a `Canvas` and add `UGUIIntegrationBootstrap`.
2. Create `UIScreenDefinition` assets for `HUD` and `PauseMenu`.
3. Assign uGUI prefabs to each definition's backend asset and set backend to `UGUI`.
4. Add `UGUIRuntimeDemo` to any scene object and assign the definitions.

## Controls

- `Escape`: toggle `PauseMenu`
- `T`: toggle between the assigned dark/light themes
- `H`: reduce the demo HP value so bound controls can react

Use `NXButtonUGUI`, `NXLabelUGUI`, and `NXProgressBarUGUI` on prefab elements that should expose NexUI capabilities.
