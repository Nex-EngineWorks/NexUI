# Sound Hooks, Core Grab-Bag, Test Harness (B5 / B7 / B9)

## B5 - Sound/transition event hooks

Already exists, no code was needed - two backend-agnostic public event buses cover both cases
the spec asked for:

- **Command Pipeline (click/action) hook**: `UICommandDispatcher.CommandExecuted` (`Action<IUICommand>`),
  raised after a command's pipeline completes. Subscribe once (e.g. in a bootstrap script) and
  play a sound based on `command.CommandId`.
- **Motion (transition) hook**: `emiteat.NexUI.Motion.MotionEvents.Started` /
  `.Completed` (`Action<string elementId, string motionId>`), raised at the start/end of every
  played motion.

Neither requires a built-in sound bank - wire your own `AudioSource.PlayOneShot` calls onto
these events from application code.

Touch/orientation detection was scoped out per the spec ("skip unless mobile is an explicit
target") - this asset has no stated mobile-only target, so it's left out.

## B7 grab-bag

- **Per-screen fault isolation** (`Runtime/Core/UIManager.cs`, `OpenAsync`/`CloseAsync`): both
  now wrap their lifecycle/motion/policy calls in `try`/`catch`. A throwing screen rolls back to
  a closed state (surface destroyed, removed from `_open`/back/modal stacks) instead of leaving
  a corrupted "stuck open forever" entry or crashing whatever called `OpenAsync`/`CloseAsync`.
  Subscribe to the new `UIManager.ScreenFaulted` event (`Action<string screenId, Exception>`) to
  show a fallback (e.g. an error toast) instead of only a console error.
- **UI state persistence** (`Runtime/Core/UISessionPersistence.cs`): opt-in - construct
  `new UISessionPersistence(manager)` once after registering screens/factories, then call
  `RestoreAsync()` at startup to reopen whatever screen was open when the session ended. Storage
  is pluggable via `IUISessionStore` (defaults to `PlayerPrefsSessionStore`); nothing changes in
  `UIManager` unless you construct this.
- **Runtime-swappable theme skins**: already exists and was already runtime-callable, not
  Designer/editor-only - `emiteat.NexUI.Theme.NexUITheme.Use("dark")` (`Runtime/Theme/NexUITheme.cs`)
  swaps the active theme and raises `ThemeEvents.ThemeChanged` for bound elements to refresh. A
  shipped game can call this directly for a player-facing light/dark toggle.
- **Deep-link navigation, Addressables-backed lazy loading**: not built in this pass. Deep-linking
  into nested UI state (e.g. "Settings > Audio tab") has no existing "tab" concept in
  `UIScreenDefinition`/Designer metadata to link into yet, and `UIScreenLoadStrategy.Addressable`
  is currently a validated-but-unimplemented no-op in `UIManager.OpenAsync` (confirmed: the
  `AddressablesUIResourceProvider` in `Integrations/Addressables` exists but nothing calls it).
  Both are real, separately-sized pieces of work - flagging rather than building a partial,
  unverified version of either.

## B9 - Test harness

`Runtime/Core/UITestHarness.cs` wraps `UIManager` + `IUICommandDispatcher` for integration
tests: `OpenAsync`/`CloseAsync`/`ToggleAsync`/`IsOpen` by ScreenID, `InvokeAsync(IUICommand)` to
dispatch the same command a click would, and `FindElement`/`ReadText`/`ReadValue` for assertions
- no `GameObject.Find`, no coordinate-based click simulation. There's intentionally no
`Click(screenId, elementId)` method: `IUIClickCapability.Clicked` is a subscribe-only event by
design, so "clicking" a button in a test means dispatching the `IUICommand` its `commandKey` is
bound to via `InvokeAsync`, exactly the same ScreenID+Command path a real click ends up calling.
