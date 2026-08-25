# Integrations

Everything backend-specific lives in `Integrations/*`. Core compiles with none of them present.

## UI Toolkit (`Integrations/UIToolkit`)

- `UIToolkitIntegrationBootstrap` (next to a `UIDocument`) registers the factory, focus adapter,
  theme applier and built-in motion player; one full-stretch container per `UILayerType`.
- Elements resolve by element **name**; capabilities wrap style properties.
- Intra-layer priority: higher `identity.priority` reorders sibling screen roots (stable).
- `OnDestroy` unregisters its layer roots, so domain reloads / scene teardown cannot leave
  dangling mount points.

## uGUI (`Integrations/UGUI`)

- `UGUIIntegrationBootstrap` (next to a `Canvas`), same registration shape.
- Each generated screen gets its own nested Canvas (rebatch isolation) unless the prefab has one.
- Input blocking uses a transparent blocker image behind content - never root
  `CanvasGroup.blocksRaycasts`, which would make children click-through.
- Base components (`NX*`): rounded rect, gradient, soft shadow, segmented bar, cooldown overlay,
  safe area, flow/radial/auto-grid layouts, marquee/typewriter/ticker text, hold button, swipe
  area, tooltip trigger, spinner/skeleton/toast/choice list, modal/popover/tooltip panel/slot,
  virtualized list/carousel/tab group. One serializable class per file - Unity maps MonoBehaviours
  to script assets by file.

### Compiled screens

`NexScreenProgram` (+ per-backend builders) is a separate compiled interaction engine used by
Studio's compiler path. It does not route through UIManager.

## DOTween (`Integrations/DOTween`, define `DOTWEEN`)

`DOTweenMotionPlayer` animates every keyframe segment of a track (not just first/last), runs on
unscaled time, kills tweens on cancellation and disposes its cancellation registrations before
UIManager disposes the linked CTS.

```csharp
manager.MotionPlayer = new DOTweenMotionPlayer();
```

## VContainer / MessagePipe

Registration samples live in `Samples~/IntegrationDemo`. Typical wiring:

```csharp
builder.RegisterInstance(NexUIApp.Manager).AsSelf();
// ScreenOpened/Closed -> MessagePipe publishers (see NexUIMessagePublisher)
```

## Addressables (`Integrations/Addressables`, versionDefine-driven)

`AddressablesUIResourceProvider` reference-counts handles: concurrent loads of one key share a
handle, and the handle is released when the owning surface is destroyed (not at creation).

Screen setup: `loadStrategy = Addressable`, `backendAsset.resourceKey = <addressables key>`.

## Input System (`Integrations/InputSystem`)

Prompt glyphs, device classes and the input-mode preview used by Studio map onto
`UIInputMode`. Set `manager.InputMode` so responsive rules with `constrainInputMode` apply.

## Optional defines summary

| Define | Source | Gates |
|---|---|---|
| `DOTWEEN` | global scripting define (DOTween asset) | Integrations/DOTween |
| `NEXUI_HAS_ADDRESSABLES` | versionDefine on com.unity.addressables | Integrations/Addressables |
