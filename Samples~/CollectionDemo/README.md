# Collection Demo

Shows `CollectionView` doing the things a list has to do in a real game: ten thousand rows without
ten thousand views, selection that survives the data changing, scroll-to, and the
content/loading/empty/error states.

The data class knows nothing about UI and the collection knows nothing about items — they meet at
`INXCollectionSource` plus a bind callback. That separation is the sample's actual subject.

## uGUI

1. Create a `Canvas` + `EventSystem`.
2. Add a **Scroll View** (`GameObject > UI > Scroll View`).
3. Add `NXCollectionView` to the Scroll View root.
4. Make a row prefab or child: any `RectTransform` with an `Image` and a `TextMeshProUGUI`. Put it
   under `Viewport/Content`, leave it inactive, and assign it to **Item Template**.
5. Add `CollectionDemoUGUI` to the same object. Optionally assign a **Status Label**.
6. Press Play.

`Load` / `Clear` / `Fail` / `RemoveSelected` / `ScrollToMiddle` are public methods — wire them to
Buttons to exercise the states.

## UI Toolkit

1. Create a GameObject with a `UIDocument` and a `PanelSettings`.
2. Add `CollectionDemoUIToolkit`. Tick **Grid** to see the same options drive a grid instead.
3. Press Play. The collection is built in code, so no UXML asset is needed.

## What to look at

| Behaviour | Where |
|---|---|
| Only the visible window exists | The status line's `realized [a..b]` never grows with the item count |
| Selection survives a data change | `RemoveSelected` — the selection re-points instead of dangling |
| Empty is derived, not set | `Clear` shows the empty state without anyone assigning it |
| Same options, both backends | `NXCollectionOptions` is identical in the two scripts |
| Grid columns from width | UI Toolkit sample with **Grid** on, then resize the Game view |

## Multi-selection modifiers

Ctrl/Cmd-click and Shift-click need a modifier probe, because reading `UnityEngine.Input` directly
throws on a project configured for the Input System package alone. With
`com.unity.inputsystem` installed it is installed automatically. On the legacy input manager:

```csharp
NXInputModifierProbe.Provider = () =>
    (Input.GetKey(KeyCode.LeftControl) ? NXInputModifiers.Additive : 0) |
    (Input.GetKey(KeyCode.LeftShift) ? NXInputModifiers.Range : 0);
```

## Not in this sample

Drag and drop, item slots and the inventory preset are not implemented yet — see the CollectionView
documentation for what is and is not supported today.
