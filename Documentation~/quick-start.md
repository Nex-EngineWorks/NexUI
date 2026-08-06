# Quick start

1. Complete [installation](installation.md).
2. Run `Tools > NexUI > Project Setup` or use Designer's Setup Doctor.
3. Add one backend to a scene:
   - UI Toolkit: `UIDocument` plus `UIToolkitIntegrationBootstrap`.
   - uGUI: `Canvas` plus `UGUIIntegrationBootstrap`.
4. Create a `UIScreenDefinition` and assign its backend asset.
5. Register and open it:

```csharp
NexUIApp.RegisterScreen(screenDefinition);
await NexUIApp.OpenAsync("HUD");
```

Designer command and state keys are identifiers. Register the matching handlers and state in
`UIActionResolver` and `UIStateStore`; authoring a string key alone does not create runtime
behavior.
