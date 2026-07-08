namespace emiteat.NexUI.Abstractions
{
    /// <summary>Marker for a dispatchable UI command.</summary>
    public interface IUICommand
    {
        string CommandId { get; }
    }
}
