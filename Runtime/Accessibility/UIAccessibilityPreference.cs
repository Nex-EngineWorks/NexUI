using System;

namespace emiteat.NexUI.Accessibility
{
    /// <summary>
    /// User-facing accessibility preferences. Read by Motion (reduce-motion),
    /// Theme (high-contrast) and layout/text systems (large-text). Backend-independent.
    /// </summary>
    [Serializable]
    public sealed class UIAccessibilityPreference
    {
        public bool reduceMotion;
        public bool highContrast;
        public bool largeText;
    }
}
