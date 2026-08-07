using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// Switches the active theme. Owned by Theme (v3 command-ownership principle) so Theme
    /// needs no reference to Core.
    /// </summary>
    public sealed class SetThemeCommand : IUICommand
    {
        public string CommandId => "theme.set";
        public string ThemeId { get; }
        public SetThemeCommand(string themeId) => ThemeId = themeId;
    }

    /// <summary>Applies a <see cref="SetThemeCommand"/> via the theme facade.</summary>
    public sealed class SetThemeCommandHandler : IUICommandHandler<SetThemeCommand>
    {
        public Task HandleAsync(SetThemeCommand command, UICommandContext context)
        {
            NexUITheme.Use(command.ThemeId);
            return Task.CompletedTask;
        }
    }
}
