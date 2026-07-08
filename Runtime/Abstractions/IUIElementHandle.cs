namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Backend-independent handle to a single UI element.
    /// Core / State / Motion only interact with elements through capabilities
    /// exposed via <see cref="As{TCapability}"/> and <see cref="Has{TCapability}"/>.
    /// </summary>
    public interface IUIElementHandle
    {
        string Id { get; }
        UIRenderBackend Backend { get; }

        /// <summary>
        /// The underlying native object (VisualElement, GameObject, Component, ...).
        /// For Debug / Integration only. Core MUST NOT cast this to a concrete type.
        /// </summary>
        object Native { get; }

        bool Has<TCapability>() where TCapability : class;
        TCapability As<TCapability>() where TCapability : class;
    }
}
