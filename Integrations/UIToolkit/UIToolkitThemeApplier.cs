using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Applies resolved theme token values onto UI Toolkit elements by delegating to the
    /// element's <see cref="IUIStyleCapability"/>. Keeps token interpretation in one place.
    /// </summary>
    public sealed class UIToolkitThemeApplier : IUIThemeApplier
    {
        public UIRenderBackend Backend => UIRenderBackend.UIToolkit;

        public void ApplyToken(IUIElementHandle target, string tokenKey, string value)
        {
            var style = target?.As<IUIStyleCapability>();
            style?.ApplyToken(tokenKey, value);
        }
    }
}
