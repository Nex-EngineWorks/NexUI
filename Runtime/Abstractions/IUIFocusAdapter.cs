namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Backend-specific focus management. Traps / releases focus for a surface so
    /// modal screens can keep keyboard / gamepad focus contained.
    /// </summary>
    public interface IUIFocusAdapter
    {
        UIRenderBackend Backend { get; }

        void Trap(IUISurface surface, string defaultElementId);
        void Release(IUISurface surface, bool restorePrevious);
    }
}
