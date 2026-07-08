using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core
{
    /// <summary>Command: open a screen by id.</summary>
    public sealed class OpenScreenCommand : IUICommand
    {
        public string CommandId => "nexui.open";
        public string ScreenId { get; }
        public UIOpenArgs Args { get; }

        public OpenScreenCommand(string screenId, UIOpenArgs args = default)
        {
            ScreenId = screenId;
            Args = args;
        }
    }

    /// <summary>Command: close a screen by id.</summary>
    public sealed class CloseScreenCommand : IUICommand
    {
        public string CommandId => "nexui.close";
        public string ScreenId { get; }
        public UICloseArgs Args { get; }

        public CloseScreenCommand(string screenId, UICloseArgs args = default)
        {
            ScreenId = screenId;
            Args = args;
        }
    }

    /// <summary>Command: toggle a screen by id.</summary>
    public sealed class ToggleScreenCommand : IUICommand
    {
        public string CommandId => "nexui.toggle";
        public string ScreenId { get; }

        public ToggleScreenCommand(string screenId) => ScreenId = screenId;
    }

    /// <summary>Command: navigate back one step in the back stack.</summary>
    public sealed class BackCommand : IUICommand
    {
        public string CommandId => "nexui.back";
    }
}
