using System;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Authoring: a named collection of steps (e.g. "open", "close", "hover").
    /// One preset can hold several variants; the compiler selects a variant by name.
    /// </summary>
    [Serializable]
    public sealed class UIMotionVariant
    {
        public string name = "default";
        public UIMotionStep[] steps = Array.Empty<UIMotionStep>();
    }
}
