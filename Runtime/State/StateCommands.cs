using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// Sets a state value by key. Owned by State (v3 command-ownership principle): the
    /// command and its handler live where the domain state lives, so State never references
    /// Core. Undoable — the inverse restores the captured previous value.
    /// </summary>
    public sealed class SetValueCommand : IUndoableCommand
    {
        public string CommandId => "state.value.set";

        public string Key { get; }
        public object Value { get; }
        public object PreviousValue { get; }

        public SetValueCommand(string key, object value, object previousValue = null)
        {
            Key = key;
            Value = value;
            PreviousValue = previousValue;
        }

        public IUICommand CreateInverse() => new SetValueCommand(Key, PreviousValue, Value);
    }

    /// <summary>Applies a <see cref="SetValueCommand"/> to a <see cref="UIStateStore"/>.</summary>
    public sealed class SetValueCommandHandler : IUICommandHandler<SetValueCommand>
    {
        private readonly UIStateStore _store;

        public SetValueCommandHandler(UIStateStore store) => _store = store;

        public Task HandleAsync(SetValueCommand command, UICommandContext context)
        {
            _store.Set(command.Key, command.Value);
            return Task.CompletedTask;
        }
    }
}
