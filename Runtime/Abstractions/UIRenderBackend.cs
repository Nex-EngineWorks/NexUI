namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Identifies which rendering backend a surface / element / factory belongs to.
    /// Core code branches on this enum only; it never touches concrete backend types.
    /// </summary>
    public enum UIRenderBackend
    {
        UIToolkit = 0,
        UGUI = 1
    }
}
