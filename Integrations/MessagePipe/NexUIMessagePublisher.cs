#if NEXUI_HAS_MESSAGEPIPE
using System;
using global::MessagePipe;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Integrations.MessagePipe
{
    /// <summary>
    /// Bridges backend-agnostic NexUI events (screen open/close, command executed, motion
    /// started/completed) onto MessagePipe publishers. Core never references MessagePipe;
    /// this integration subscribes to Core's events and re-publishes.
    /// </summary>
    public sealed class NexUIMessagePublisher : IDisposable
    {
        private readonly UIManager _manager;
        private readonly UICommandDispatcher _dispatcher;

        private readonly IPublisher<UIOpenedMessage> _opened;
        private readonly IPublisher<UIClosedMessage> _closed;
        private readonly IPublisher<ToastShownMessage> _toast;
        private readonly IPublisher<UICommandExecutedMessage> _command;
        private readonly IPublisher<MotionStartedMessage> _motionStarted;
        private readonly IPublisher<MotionCompletedMessage> _motionCompleted;

        public NexUIMessagePublisher(
            UIManager manager,
            UICommandDispatcher dispatcher,
            IPublisher<UIOpenedMessage> opened,
            IPublisher<UIClosedMessage> closed,
            IPublisher<ToastShownMessage> toast,
            IPublisher<UICommandExecutedMessage> command,
            IPublisher<MotionStartedMessage> motionStarted,
            IPublisher<MotionCompletedMessage> motionCompleted)
        {
            _manager = manager;
            _dispatcher = dispatcher;
            _opened = opened;
            _closed = closed;
            _toast = toast;
            _command = command;
            _motionStarted = motionStarted;
            _motionCompleted = motionCompleted;

            if (_manager != null)
            {
                _manager.ScreenOpened += OnScreenOpened;
                _manager.ScreenClosed += OnScreenClosed;
            }
            if (_dispatcher != null)
                _dispatcher.CommandExecuted += OnCommandExecuted;

            UIMotionEvents.Started += OnMotionStarted;
            UIMotionEvents.Completed += OnMotionCompleted;
        }

        private void OnScreenOpened(UIScreenInstance inst)
        {
            _opened?.Publish(new UIOpenedMessage(inst.ScreenId, inst.Layer));
            if (inst.Layer == UILayerType.Toast)
                _toast?.Publish(new ToastShownMessage(inst.ScreenId));
        }

        private void OnScreenClosed(UIScreenInstance inst)
            => _closed?.Publish(new UIClosedMessage(inst.ScreenId));

        private void OnCommandExecuted(IUICommand command)
            => _command?.Publish(new UICommandExecutedMessage(command.CommandId));

        private void OnMotionStarted(string elementId, string motionId)
            => _motionStarted?.Publish(new MotionStartedMessage(elementId, motionId));

        private void OnMotionCompleted(string elementId, string motionId)
            => _motionCompleted?.Publish(new MotionCompletedMessage(elementId, motionId));

        public void Dispose()
        {
            if (_manager != null)
            {
                _manager.ScreenOpened -= OnScreenOpened;
                _manager.ScreenClosed -= OnScreenClosed;
            }
            if (_dispatcher != null)
                _dispatcher.CommandExecuted -= OnCommandExecuted;

            UIMotionEvents.Started -= OnMotionStarted;
            UIMotionEvents.Completed -= OnMotionCompleted;
        }
    }
}
#endif
