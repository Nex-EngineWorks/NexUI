using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// UI Toolkit label wrapper. There is no INXLabel contract in the Components module;
    /// this thin wrapper exposes text/visibility over an element handle for convenience.
    /// </summary>
    public sealed class NXLabelUIToolkit
    {
        private readonly IUITextCapability _text;
        private readonly IUIVisibilityCapability _visibility;

        public IUIElementHandle Handle { get; }

        public NXLabelUIToolkit(IUIElementHandle handle)
        {
            Handle = handle;
            _text = handle?.As<IUITextCapability>();
            _visibility = handle?.As<IUIVisibilityCapability>();
        }

        public string Text
        {
            get => _text?.Text ?? string.Empty;
            set { if (_text != null) _text.Text = value; }
        }

        public bool Visible
        {
            get => _visibility?.Visible ?? false;
            set { if (_visibility != null) _visibility.Visible = value; }
        }
    }
}
