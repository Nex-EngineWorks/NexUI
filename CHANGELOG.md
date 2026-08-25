# Changelog

All notable changes to this package are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added
- **Compiled screens open through the UIManager**: both integration bootstraps now register a
  delegating factory (`NexCompiledUguiScreenFactory` / `NexCompiledUitoolkitScreenFactory`). A
  screen whose backend asset is a `NexScreenProgram` is built by the compiled engine and wrapped
  so the UIManager lifecycle (layers, policies, stacks, results, WaitForCloseAsync) drives it;
  destroying the surface disposes the runtime. Regular prefab/UXML assets delegate to the
  original factories untouched - the two screen systems finally share one entry point.
- **ReplaceLayer crossfade**: sibling screens now play their exit motion in PARALLEL with the
  new screen's open motion (previously sequential - exit fully, then enter). The layer is awaited
  to a settled state before OnAfterOpen fires, so ordering stays deterministic.
- **Stack snapshot / restore**: `CaptureStackSnapshot()` records every open screen (bottom → top,
  with its last open args/variant); `RestoreStackAsync(snapshot)` closes everything and reopens in
  capture order, letting each screen's own policy re-push back/modal stacks. In-process session
  resume for "quit and continue later".
- `UGUISurface.Destroy` uses DestroyImmediate in edit mode (matching NexScreenRuntime), so
  edit-time tooling and tests no longer trip deferred-destroy semantics.
- **Dialog-style request/response navigation**: `UICloseArgs.result` plus
  `UIManager.WaitForCloseAsync(screenId, ct)` - `var picked = await ui.WaitForCloseAsync("ItemPicker");`
  completes with whatever the closer handed back. Awaiting an already-closed screen returns its
  last recorded result immediately (never deadlocks), and Shutdown releases waiters.
- **Back with a result**: `BackAsync<TResult>(result)` - the back gesture can now carry a value to
  `WaitForCloseAsync` waiters, same as an explicit close.
- `CloseOthersAsync(keepScreenId)` - "focus mode": close everything except one screen, across all
  layers.
- Bulk close: `CloseAllAsync` and `CloseLayerAsync(layer)` - "return to lobby", "close all
  popups" - snapshot-first so relations closing mid-loop stay safe.
- `UIScreenRegistry.Unregister(screenId)` / `UIManager.UnregisterScreen`: remove a registration;
  open instances are untouched.
- Compiled uGUI and UI Toolkit motion playback through an optional runtime registry/player,
  including entry, pointer, focus, and awaitable exit variants.
- `OpenAsync`/`CloseAsync`/`PreloadAsync` accept an optional `CancellationToken`: cancelling rolls
  the operation back and surfaces as a cancelled task on the caller's side.
- `UIScreenContext.Payload`/`VariantId` - screens finally receive the data passed to `OpenAsync`
  (the `UIOpenArgs.payload` field existed but was never delivered).
- `NexUIApp.Reset()` shuts down and drops the shared manager; required between
  domain-reload-free sessions and after tests.
- `UIManager.UnregisterLayer`, `UILayerManager.UnregisterLayer` (reference-checked), and
  `OnDestroy` teardown in both integration bootstraps so destroyed bootstraps stop receiving
  screens.
- `UISessionPersistence.Dispose` - unsubscribes from manager events instead of leaking them.
- Theme propagation: switching theme or setting a token override now re-applies tokens to every
  open screen through `UIOpenSurfaceRegistry` (Abstractions bridge - no Core→Theme dependency).
- UI Toolkit intra-layer priority: `SetSortingOrder` now reorders sibling screen roots by recorded
  order (stable) instead of only calling BringToFront.

### Changed
- `ReplaceLayer` closes sibling screens with their authored close motion (previously forced
  `immediate = true`, skipping exit animations entirely).
- uGUI pointer and focus capabilities backed by EventSystem callbacks.
- Two-way text/value binding modes, backend input capabilities, and keyed forward/back converter registry.
- Live `UIScreenContract` capability validation against resolved screen surfaces.
- Motion Graph parallel completion policies: all, any, and fire-and-forget.
- Public configuration for rounded rectangles, gradients, and soft shadows used by Studio backend generation.
- Windows, macOS, and Linux package validation, including portable paths, filenames, assemblies, and runtime source checks.

### Added (Studio)
- **Undo/Redo/Save keyboard shortcuts**: Ctrl+Z / Ctrl+Y / Ctrl+S now work inside the Studio
  canvas (previously only Unity's menu bar handled them, which required leaving the canvas
  focus). Wired through the existing shortcut registry so users can rebind them.
- **Element Diff viewer** (element context menu > Compare With ▸): pick a second element and get
  a read-only listing of every differing property - identity, rect, layout, style, typography,
  all six binding channels. Pure metadata scan, unit-tested.
- **Validation quick-fixes extended**: `collection-template-missing` gains "Create item template
  child" (creates a Panel in the collection's first declared template slot, safe/undoable), and
  `unsupported-binding` gains "Remove unsupported binding keys" (confirm-gated; recomputes the
  unsupported channels rather than parsing issue text). All existing fix labels are localized.
- **Autosave recovery banner**: when an autosave snapshot for the open document is NEWER than the
  asset on disk (the crash signature), a slim banner appears above the canvas with Restore /
  Discard. Everyday autosave churn never triggers it - dirty sessions and post-save states stay
  quiet.
- **PNG preview export** (More ⋮ > "Export PNG Preview (uGUI)"): renders the screen's saved uGUI
  prefab at the current design resolution through an off-screen camera into a transparent-
  background PNG next to the metadata, then reveals it. Temporary canvas/camera are destroyed in
  the same call - a failed export leaves no scene objects behind.
- **Find Usages** (element context menu): scans the open document for everything pointing at an
  element - direct children plus element references inside component property bags (UnityEvent
  targets, wired sibling fields) - lists `source ← where` and jumps to the source on click.
- **Inspector tooltips localized** (~32 remaining hardcoded strings across Layout / Auto Layout /
  Style / Component Parts / Interaction inspectors, ko/en) plus the last 7 stragglers in
  Binding / Component / Component Instance / Accessibility inspectors — the Inspector is now
  fully localized on both languages with no hardcoded English tooltips left.
- **Multi-window editing verified & documented**: each Studio window owns an independent context
  (own screen, undo history, validation state, autosave snapshot); satellite tools follow the
  focused window. See Documentation~/project-setup.md.
- **Crash recovery (autosave)**: while a document is edited, throttled JSON snapshots land in
  `Library/NexUIStudio/Autosave`; after a crash the More (⋮) menu offers "Restore Autosave - {name}
  (saved {time})". Restore is Undo-able, snapshots are cleared on a successful save, and garbage
  snapshots fail gracefully without touching the asset.
- **Searchable screen switcher in the global toolbar** (▾ next to the Screen field): every screen
  in the project, recently opened ones pinned to the top with a checkmark on the current one, plus
  "Clear Recent Screens". Switching screens no longer requires a Project-window hunt.
- **English is now the default language** (Korean remains fully supported via Tools > NexUI
  Studio > Language, and an explicitly stored choice is still honored).
- Figma Bridge window localized (33 ko/en keys) - it was Korean-only hardcoded.
- Localization files are strictly valid JSON again: ko-KR.json had 491 lines of byte-level
  encoding damage that silently disabled Korean text at runtime; the file was rebuilt against the
  en-US key set, preserving every intact Korean entry (169) and filling the rest with the English
  value so both languages stay key-complete.
- Component library: the Patterns family nests its cards under a category foldout like every other
  family, keeping palette invariants uniform.
- Component library: the "Recent" section tracks what this user actually placed (persisted,
  newest first, capped) instead of a hardcoded guess, and hides itself until something was placed.

### Changed (Studio)
- **Incremental viewport refresh**: property edits now update only the touched element's view
  instead of tearing down and rebuilding every canvas view. A per-slot signature (authored id +
  preview identity + rect) gates the fast path; structural moves, expansion size changes,
  resolution/input switches and any unexpected drift deterministically fall back to the full
  rebuild, which re-baselines the cache. Typing in an inspector field no longer repaints the
  whole hierarchy.
- Editor performance: fragment-id resolution and screen→metadata resolution use indexes
  invalidated by asset post-processing - opening a screen or compiling fragments no longer scans
  and loads every asset of that type per call.
- Rect edits (move/resize/align/distribute) are now tracked as per-element property changes and
  defer validation to the end of the drag gesture (or the next save) instead of running a full
  pass per pointer-move. Unlike the previously reverted delayCall attempt, the flush is called
  deterministically by the gesture itself - no editor-loop timing, no undo interaction.
- Canvas wheel zoom is multiplicative (×1.1 steps) with anchor preserved; additive ±0.08 felt
  glacial at low zoom and hypersensitive at high zoom.
- A clean save no longer logs a full report to the Console on every Ctrl+S (opt back in via
  `NexUI.Designer.Save.LogClean`); errors/warnings still always log.
- Validation: a collection preset used statically (hand-placed children, nothing bound) no longer
  raises data-driven collection errors.
- Viewport canvas empty states, hint bar, ruler/rotate tooltips and floating toolbar labels are
  localized (ko/en).

### Fixed (Studio)
- **uGUI BaseComponents**: every multi-class file (`NXGraphics`, `NXCollections`, `NXInteractions`,
  `NXLayouts`, `NXTextEffects`, `NXFeedback`, `NXOverlays`, `NXCollectionView` relay, Studio
  fragment preset) was split into one serializable class per file. Unity maps a MonoBehaviour to
  its script asset by file, so any component that was not the first class in these files saved as
  a missing-script reference (`m_Script: {fileID: 0}`) - drop shadows, gradients, carousels, tab
  groups, swipe areas, flow/radial/auto-grid layouts, marquee/typewriter/ticker text, spinners,
  skeletons, toasts, choice lists, popovers, tooltip panels and slots were all silently broken
  after a prefab round trip.
- **UnityEvent wiring**: resolving an element reference whose field type is `UnityEngine.Object`
  crashed the save with `GetComponent(Object)`; the fallback is now guarded.
- **Inspector stack order** test polluted by the machine-wide workspace-mode pref; the suite now
  isolates it.
- **Sample smoke tests** depended on another fixture registering designer backends; they now seed
  the registry themselves.
- Editor windows paused their one-shot `schedule.Execute` items (scroll restore, zoom-to-selection
  pin, initial focus, chat autoscroll, provider switch rebuild, split-view defaults) which had
  been re-running every frame and fighting user input.

### Fixed
- UI Toolkit numeric style parsing now uses invariant culture so decimal values behave consistently on every host locale.
- **UIPolicyRunner**: the slow-motion factor is configurable (`UIPolicyRunner.SlowWhileOpenScale`,
  default 0.25) and documented - Time.timeScale ownership belongs to the runner from the first
  policy until the last screen closes.
- **BuiltInMotionPlayer**: a cancelled animation now snaps to the authored end pose instead of
  stranding the element mid-flight (a cancelled open used to leave screens at partial opacity).
- **State**: `UIStateStore.Set`/`UISignal.Value` notifications no longer allocate a snapshot array
  per change; watermark + deferred-removal keeps the tested unsubscribe-during-dispatch semantics.
- **UIManager**: `CloseLayerExceptAsync` walks the open map directly instead of LINQ per ReplaceLayer open.
- **UI Toolkit transforms**: `Position`/`Rotation`/`Opacity` reads prefer an explicit inline style
  value over the resolved style, so animation loops read back what they just wrote.
- **UIManager**: a queued toast open dropped by the `Ignore` transition-conflict policy no longer
  leaks the toast slot (the queue previously stalled forever; regression-tested in
  `UIManagerRegressionTests`).
- **UIManager**: rolling back a failed open now returns KeepAlive/Pool/Preload surfaces to the
  retained cache instead of destroying them, honoring their lifetime contract.
- **Addressables**: concurrent loads of the same key share one handle and handles are
  reference-counted; the provider handle is released when the surface is destroyed rather than
  immediately after creation (previously unloading shared assets under live screens and leaking
  handles on duplicate concurrent loads).
- **uGUI**: `SetInputBlocking(false)` no longer makes the whole screen click-through — the root
  `CanvasGroup.blocksRaycasts` stays permeable and a dedicated transparent blocker image behind
  the content toggles instead.
- **DOTween player**: cancellation registrations are disposed before the linked CTS is disposed,
  and all keyframes are animated (previously only the first/last keyframe of each track played).

### Added (Motion Clip)
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
