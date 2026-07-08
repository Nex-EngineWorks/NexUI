namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// A command that can produce its own inverse, enabling undo and reversible replay.
    /// Lives in Abstractions so any module (State/Motion/Theme) can author undoable
    /// commands without referencing Core. The inverse must be fully self-contained.
    /// </summary>
    public interface IUndoableCommand : IUICommand
    {
        IUICommand CreateInverse();
    }
}
