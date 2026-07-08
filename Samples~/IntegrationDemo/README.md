# NexUI Integration Demo

This sample is a reference map for optional integrations. The sample assembly deliberately avoids hard references to optional third-party packages so it imports safely into projects that do not use them.

## Optional Modules

- DOTween: replace `UIManager.MotionPlayer` with `DOTweenMotionPlayer`.
- VContainer: call `RegisterNexUI(settings)` from a lifetime scope.
- MessagePipe: bridge `UIManager` events into `UIOpenedMessage`, `UIClosedMessage`, and command/motion messages.
- Addressables: use `AddressablesUIResourceProvider` where screen assets are loaded by key.
- Input System: register `InputSystemPolicy` to switch gameplay/UI action maps during screen open/close.

Open `IntegrationDemoBootstrap` in the inspector to see which optional assemblies are currently available in the project.
