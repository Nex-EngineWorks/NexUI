using System;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// A named variation of a <see cref="UIScreenDefinition"/> that applies a set of
    /// property overrides on top of the base screen (e.g. Compact, ControllerMode,
    /// Combat). Selected at open time via <see cref="UIOpenArgs.variantId"/>.
    /// </summary>
    [Serializable]
    public sealed class UIScreenVariant
    {
        public string variantId;
        public string displayName;

        public UIScreenVariantOverride[] overrides;
    }

    /// <summary>
    /// A single property override applied by a <see cref="UIScreenVariant"/>.
    /// <paramref name="value"/> is stored as a string and parsed by the backend that
    /// owns <paramref name="propertyPath"/>, so Core stays backend-independent.
    /// </summary>
    [Serializable]
    public sealed class UIScreenVariantOverride
    {
        public string targetElementId;
        public string propertyPath;
        public string value;
    }
}
