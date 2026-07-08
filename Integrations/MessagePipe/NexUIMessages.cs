#if NEXUI_HAS_MESSAGEPIPE
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Integrations.MessagePipe
{
    public readonly struct UIOpenedMessage
    {
        public readonly string ScreenId;
        public readonly UILayerType LayerType;
        public UIOpenedMessage(string screenId, UILayerType layerType)
        { ScreenId = screenId; LayerType = layerType; }
    }

    public readonly struct UIClosedMessage
    {
        public readonly string ScreenId;
        public UIClosedMessage(string screenId) => ScreenId = screenId;
    }

    public readonly struct ToastShownMessage
    {
        public readonly string Message;
        public ToastShownMessage(string message) => Message = message;
    }

    public readonly struct MotionStartedMessage
    {
        public readonly string ElementId;
        public readonly string MotionId;
        public MotionStartedMessage(string elementId, string motionId)
        { ElementId = elementId; MotionId = motionId; }
    }

    public readonly struct MotionCompletedMessage
    {
        public readonly string ElementId;
        public readonly string MotionId;
        public MotionCompletedMessage(string elementId, string motionId)
        { ElementId = elementId; MotionId = motionId; }
    }

    public readonly struct UICommandExecutedMessage
    {
        public readonly string CommandId;
        public UICommandExecutedMessage(string commandId) => CommandId = commandId;
    }
}
#endif
