# Optional integrations

NexUI keeps optional adapters in separate assemblies for DOTween, Addressables, Input System,
MessagePipe and VContainer. Only enable an integration when its third-party package is installed.

The core runtime remains backend independent. UI Toolkit and uGUI implementations live in
`Integrations/UIToolkit` and `Integrations/UGUI` respectively.

Before distributing a product, record the exact dependency versions and licenses used by the
project. NexUI's direct required third-party dependency is listed in `Third Party Notices.txt`.
