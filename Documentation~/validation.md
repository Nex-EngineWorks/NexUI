# Validation

Three layers, one vocabulary of issue codes.

## 1. Authoring-time (Studio)

`Tools/NexUI/Validator` and the Issues pane run `DesignerValidationService` whenever metadata
changes. Highlights:

| Code | Severity | Meaning |
|---|---|---|
| `duplicate-screen-id` | Error | Two registered screens share an id |
| `missing-asset` | Error | Backend asset / motion / theme reference is null |
| `backend-mismatch` | Error | Element type not supported by the screen's backend |
| `collection-template-missing` | Error | CollectionView preset with data bound but no item template |
| `unsupported-binding` | Info | Key set on a channel the element type ignores (kept, surfaced in Advanced) |
| `orphan-backend-element` | Info | Prefab object without Designer metadata |

A collection preset used **statically** (hand-placed children, nothing bound) intentionally raises
no collection errors.

## 2. Registration-time (runtime)

`UIManager.RegisterScreen` warns on duplicate ids; validators like
`ScreenFactoryAvailabilityValidator` catch "backend never registered" before Play.

## 3. Live contract checks

`UIScreenContract` validation compares what a screen's compiled surface actually exposes
(capabilities per element) against the authored contract, so a renamed id or removed component
surfaces as an error the moment it stops matching - not as a null reference at runtime.

## Running validation yourself

```csharp
var issues = DesignerValidationService.Validate(screenDefinition, metadata);
foreach (var issue in issues.Where(i => i.Severity >= DesignerValidationSeverity.Warning))
    Debug.Log(issue);
```

Issue shape: `(severity, code, message, fixHint, screenId, elementId)` - codes are stable strings,
safe to grep in CI logs.

## Studio integration

- Toolbar status chip colors by worst severity; click opens Issues.
- Save blocks on Error-severity issues unless you fix them (warnings pass).
- Per-element results are cached by document revision, so moving one element revalidates cheaply;
  pure rect drags defer validation until the gesture ends.
