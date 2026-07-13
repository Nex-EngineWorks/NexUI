using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// Executes a <see cref="UIMotionStateMachine"/> against one <see cref="IUISurface"/>: tracks
    /// the current <see cref="UIMotionState"/>, resolves the transition clip for a requested state
    /// change, and applies its <see cref="UIMotionStateInterruptPolicy"/> if another transition is
    /// still playing. One runner per animated element/component instance (it holds per-instance
    /// playback state); the <see cref="UIMotionStateMachine"/> asset itself is shared.
    /// </summary>
    public sealed class UIMotionStateRunner
    {
        private readonly IUIMotionClipPlayer _player;
        private UIMotionClip _activeClip;
        private bool _isTransitioning;

        public UIMotionState CurrentState { get; private set; }

        public UIMotionStateRunner(UIMotionState initialState = UIMotionState.Normal, IUIMotionClipPlayer player = null)
        {
            CurrentState = initialState;
            _player = player ?? new UIMotionClipPlayer();
        }

        /// <summary>
        /// Requests a transition to <paramref name="to"/>. If no transition is authored for the
        /// current state (nor an Any-State fallback), the state still updates but nothing plays -
        /// callers shouldn't have to special-case "no motion for this state change".
        /// </summary>
        public async UniTask TransitionToAsync(IUISurface surface, UIMotionStateMachine machine, UIMotionState to,
            CancellationToken cancellationToken = default)
        {
            if (machine == null)
            {
                CurrentState = to;
                return;
            }

            var transition = machine.FindTransition(CurrentState, to);
            if (transition?.clip == null)
            {
                CurrentState = to;
                return;
            }

            if (_isTransitioning)
            {
                switch (transition.interruptPolicy)
                {
                    case UIMotionStateInterruptPolicy.Ignore:
                        return;

                    case UIMotionStateInterruptPolicy.CompleteImmediately:
                        _player.Stop();
                        if (_activeClip != null)
                            _player.Evaluate(surface, _activeClip, _activeClip.duration);
                        break;

                    case UIMotionStateInterruptPolicy.Restart:
                    default:
                        _player.Stop();
                        break;
                }
            }

            _isTransitioning = true;
            _activeClip = transition.clip;
            CurrentState = to;

            try
            {
                await _player.PlayAsync(surface, transition.clip, cancellationToken);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public void Stop()
        {
            _player.Stop();
            _isTransitioning = false;
        }
    }
}
