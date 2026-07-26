# Installation

## Supported order

NexUI exposes UniTask in its public API. Install dependencies in this order so Unity never
imports NexUI while `UniTask` is unresolved.

1. Install UniTask 2.5.10 or newer from
   `https://github.com/Cysharp/UniTask.git?path=src/UniTask`.
2. Install NexUI Runtime from `https://github.com/swallow-smoke/NexUI.git`.
3. Optional: install NexUI Designer from
   `https://github.com/swallow-smoke/NexUI-Designer.git`.
4. Open `Tools > NexUI > Utilities > Setup Doctor` and resolve every error.

For a local checkout, add the package roots with Package Manager's **Add package from disk**
and select each `package.json` in the same order.

## Why UniTask is not in `dependencies`

Unity package dependencies use registry package names and versions. UniTask is distributed as
a Git package rather than through Unity's default registry, so declaring only
`"com.cysharp.unitask": "2.5.10"` would still fail in an unconfigured project. NexUI therefore
documents and diagnoses this prerequisite explicitly.

## Clean-install acceptance check

- Unity Console contains no NexUI-originated errors or warnings after import.
- Setup Doctor reports no errors.
- At least one backend bootstrap exists in the test scene.
- The Basic Runtime sample opens and closes a screen in Play Mode.
- The intended Player target builds successfully.
