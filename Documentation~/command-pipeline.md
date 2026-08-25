# Command Pipeline

Clicks do not call game code directly. A Studio-authored `commandKey` is dispatched at runtime
through a small, testable pipeline.

## Flow

```
uGUI Button / UI Toolkit Button
  └─ NXInteractionRelay / VeClick
       └─ UIActionResolver  (commandKey → IUICommand)
            └─ UICommandDispatcher
                 ├─ middleware (outer → inner)
                 ├─ IUICommandHandler<TCommand>
                 └─ CommandLog (undoable commands recorded for replay)
```

## Dispatching from game code

```csharp
var dispatcher = new UICommandDispatcher();
dispatcher.UseMiddleware(new LoggingMiddleware());
dispatcher.RegisterHandler(new OpenInventoryHandler());

await dispatcher.DispatchAsync(new OpenInventoryCommand { Tab = "Gear" });
```

`UIActionResolver` bridges the two worlds: give it your dispatcher and any commandKeys already
used in screens resolve automatically. Studio's Binding inspector offers existing keys via Pick.

## Undoable commands

Implement `IUndoableCommand` and the log gains Do/Undo/Redo plus replay:

```csharp
public sealed class GrantGoldCommand : IUndoableCommand
{
    public int Amount;
    public void Do()   => Wallet.Add(Amount);
    public void Undo() => Wallet.Add(-Amount);
}
```

`CommandReplay` re-runs a recorded sequence against a fresh session - useful for bug reports
("attach the command log") and smoke tests.

## Relation to screens

- Screen definitions may declare `relations.closes` / `opensWith`, so one command can choreograph
  navigation without game-side knowledge of specific ids.
- `policy.closeOnBack` makes BackAsync treat the screen like a stack entry even outside StackPush.
- Results: close with `UICloseArgs.result` and await `WaitForCloseAsync` - see the beginner handbook.
