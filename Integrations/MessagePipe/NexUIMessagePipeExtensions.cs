#if NEXUI_HAS_MESSAGEPIPE
using global::MessagePipe;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Integrations.MessagePipe
{
    /// <summary>Convenience wiring for the MessagePipe bridge.</summary>
    public static class NexUIMessagePipeExtensions
    {
        /// <summary>
        /// Create and return a publisher bridge from an <see cref="IServiceProvider"/> that can
        /// resolve MessagePipe <c>IPublisher&lt;T&gt;</c> instances (e.g. the built provider).
        /// Keep the returned instance alive; dispose it to unsubscribe.
        /// </summary>
        public static NexUIMessagePublisher CreateNexUIMessageBridge(
            this System.IServiceProvider provider,
            UIManager manager,
            UICommandDispatcher dispatcher)
        {
            return new NexUIMessagePublisher(
                manager,
                dispatcher,
                (IPublisher<UIOpenedMessage>)provider.GetService(typeof(IPublisher<UIOpenedMessage>)),
                (IPublisher<UIClosedMessage>)provider.GetService(typeof(IPublisher<UIClosedMessage>)),
                (IPublisher<ToastShownMessage>)provider.GetService(typeof(IPublisher<ToastShownMessage>)),
                (IPublisher<UICommandExecutedMessage>)provider.GetService(typeof(IPublisher<UICommandExecutedMessage>)),
                (IPublisher<MotionStartedMessage>)provider.GetService(typeof(IPublisher<MotionStartedMessage>)),
                (IPublisher<MotionCompletedMessage>)provider.GetService(typeof(IPublisher<MotionCompletedMessage>)));
        }
    }
}
#endif
