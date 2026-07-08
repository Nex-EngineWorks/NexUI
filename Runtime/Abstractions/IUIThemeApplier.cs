namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Backend-specific application of a resolved theme token value onto an element.
    /// Theme resolution is backend-independent; only the final apply step is per-backend.
    /// </summary>
    public interface IUIThemeApplier
    {
        UIRenderBackend Backend { get; }

        void ApplyToken(
            IUIElementHandle target,
            string tokenKey,
            string value
        );
    }
}
