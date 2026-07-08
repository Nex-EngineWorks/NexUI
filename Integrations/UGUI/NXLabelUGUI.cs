using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// uGUI label wrapper (TMP_Text or legacy Text). No INXLabel contract exists in the
    /// Components module; this exposes text/visibility over an element handle.
    /// </summary>
    public sealed class NXLabelUGUI
    {
        private readonly IUITextCapability _text;
        private readonly IUIVisibilityCapability _visibility;

        public IUIElementHandle Handle { get; }

        public NXLabelUGUI(IUIElementHandle handle)
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
