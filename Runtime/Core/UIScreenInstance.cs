using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core
{
    public enum UIScreenState
    {
        Created = 0,
        Opening = 1,
        Open = 2,
        Closing = 3,
        Closed = 4
    }

    /// <summary>
    /// A live screen: pairs a definition with its instantiated surface and tracks
    /// runtime state. The optional lifecycle handler is discovered from the surface
    /// root's native controller by the Integration and forwarded here.
    /// </summary>
    public sealed class UIScreenInstance
    {
        public UIScreenDefinition Definition { get; }
        public IUISurface Surface { get; }
        public UILayerType Layer => Definition.layer.layerType;
        public string ScreenId => Definition.ScreenId;

        public UIScreenState State { get; internal set; }
        public IUIScreenLifecycle Lifecycle { get; internal set; }

        public UIScreenInstance(UIScreenDefinition definition, IUISurface surface)
        {
            Definition = definition;
            Surface = surface;
            State = UIScreenState.Created;

            // Discover an optional lifecycle handler from the native controller.
            Lifecycle = surface?.RootHandle?.Native as IUIScreenLifecycle
                        ?? surface as IUIScreenLifecycle;
        }
    }
}
