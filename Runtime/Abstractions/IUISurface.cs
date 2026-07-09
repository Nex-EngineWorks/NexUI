namespace emiteat.NexUI.Abstractions
{
    /// <summary>Thrown by <see cref="IUISurface.FindRequired"/> when an element id is absent.</summary>
    public sealed class UIElementNotFoundException : System.Exception
    {
        public UIElementNotFoundException(string elementId)
            : base($"Element '{elementId}' was not found on this surface.")
        {
        }
    }

    /// <summary>
    /// Backend-independent representation of one instantiated screen / view tree.
    /// A surface owns its native root and can resolve child element handles by id.
    /// </summary>
    public interface IUISurface
    {
        string ScreenId { get; }
        UIRenderBackend Backend { get; }

        /// <summary>Native root (VisualElement / GameObject). Integration-only.</summary>
        object NativeRoot { get; }

        IUIElementHandle RootHandle { get; }

        /// <summary>Returns the element handle, or <c>null</c> if not found.</summary>
        IUIElementHandle TryFind(string elementId);

        /// <summary>Returns the element handle, or throws <see cref="UIElementNotFoundException"/>.</summary>
        IUIElementHandle FindRequired(string elementId);

        void SetActive(bool active);
        void SetSortingOrder(int order);
        void SetInputBlocking(bool blocking);
        void Destroy();
    }
}
