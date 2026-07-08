using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Applies resolved theme token values onto uGUI elements by delegating to the
    /// element's <see cref="IUIStyleCapability"/> (color tokens map to Graphic color).
    /// </summary>
    public sealed class UGUIThemeApplier : IUIThemeApplier
    {
        public UIRenderBackend Backend => UIRenderBackend.UGUI;

        public void ApplyToken(IUIElementHandle target, string tokenKey, string value)
        {
            var style = target?.As<IUIStyleCapability>();
            style?.ApplyToken(tokenKey, value);
        }
    }
}
