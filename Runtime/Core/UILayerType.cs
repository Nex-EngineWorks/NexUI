namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Logical stacking layers. Higher values render above lower ones. The concrete
    /// mapping to sorting order / canvas / panel settings is done by the Integration.
    /// </summary>
    public enum UILayerType
    {
        Background = 0,
        Scene = 10,
        HUD = 20,
        Window = 30,
        Modal = 40,
        Toast = 50,
        Overlay = 60,
        System = 70
    }
}
