using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Wires an element's optional pointer / focus capabilities to immediate gesture motion
    /// (hover lift, press scale, focus emphasis). Degrades gracefully when a backend does
    /// not provide the capability.
    /// </summary>
    public sealed class GestureMotionController
    {
        private readonly IUIElementHandle _target;
        private readonly IUITransformCapability _transform;

        public float PressScale { get; set; } = 0.95f;
        public float HoverScale { get; set; } = 1.03f;

        public GestureMotionController(IUIElementHandle target)
        {
            _target = target;
            _transform = target?.As<IUITransformCapability>();
        }

        /// <summary>Subscribe to available gesture sources. Returns this for chaining.</summary>
        public GestureMotionController Attach()
        {
            var pointer = _target?.As<IUIPointerCapability>();
            if (pointer != null && _transform != null)
            {
                pointer.PointerEntered += () => SetScale(HoverScale);
                pointer.PointerExited += () => SetScale(1f);
                pointer.PointerDown += () => SetScale(PressScale);
                pointer.PointerUp += () => SetScale(HoverScale);
            }

            var focus = _target?.As<IUIFocusCapability>();
            if (focus != null && _transform != null)
            {
                focus.Focused += () => SetScale(HoverScale);
                focus.Blurred += () => SetScale(1f);
            }

            return this;
        }

        private void SetScale(float s)
        {
            if (_transform == null) return;
            _transform.Scale = new UnityEngine.Vector3(s, s, 1f);
        }
    }
}
