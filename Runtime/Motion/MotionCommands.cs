using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Plays a compiled timeline on an element. Owned by Motion (v3 command-ownership
    /// principle) so Motion needs no reference to Core.
    /// </summary>
    public sealed class PlayMotionCommand : IUICommand
    {
        public string CommandId => "motion.play";

        public IUIElementHandle Target { get; }
        public UIMotionTimeline Timeline { get; }

        public PlayMotionCommand(IUIElementHandle target, UIMotionTimeline timeline)
        {
            Target = target;
            Timeline = timeline;
        }
    }

    /// <summary>Runs a <see cref="PlayMotionCommand"/> through the registered motion player.</summary>
    public sealed class PlayMotionCommandHandler : IUICommandHandler<PlayMotionCommand>
    {
        private readonly IUIMotionPlayer _player;

        public PlayMotionCommandHandler(IUIMotionPlayer player) => _player = player;

        public UniTask HandleAsync(PlayMotionCommand command, UICommandContext context)
            => _player.PlayAsync(command.Target, command.Timeline, CancellationToken.None);
    }
}
