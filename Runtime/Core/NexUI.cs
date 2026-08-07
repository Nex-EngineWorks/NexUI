using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Static convenience facade over a single shared <see cref="UIManager"/>.
    /// Games that want dependency injection can ignore this and use UIManager directly.
    /// </summary>
    public static class NexUIApp
    {
        private static UIManager _manager;

        public static UIManager Manager => _manager ??= new UIManager();

        /// <summary>Replace the shared manager (e.g. from a DI container / bootstrap).</summary>
        public static void SetManager(UIManager manager) => _manager = manager;

        // ---- Async API ------------------------------------------------------

        public static Task OpenAsync(string screenId, UIOpenArgs args = default) => Manager.OpenAsync(screenId, args);
        public static Task CloseAsync(string screenId, UICloseArgs args = default) => Manager.CloseAsync(screenId, args);
        public static Task ToggleAsync(string screenId) => Manager.ToggleAsync(screenId);
        public static Task BackAsync() => Manager.BackAsync();
        public static Task PreloadAsync() => Manager.PreloadAsync();
        public static Task PreloadAsync(string screenId) => Manager.PreloadAsync(screenId);

        // ---- Fire-and-forget sync API --------------------------------------

        public static void Open(string screenId) => _ = Manager.OpenAsync(screenId);
        public static void Close(string screenId) => _ = Manager.CloseAsync(screenId);
        public static void Toggle(string screenId) => _ = Manager.ToggleAsync(screenId);
        public static void Back() => _ = Manager.BackAsync();

        // ---- Queries / registration ----------------------------------------

        public static bool IsOpen(string screenId) => Manager.IsOpen(screenId);

        public static void RegisterScreen(UIScreenDefinition definition) => Manager.RegisterScreen(definition);
        public static void RegisterFactory(IUIScreenFactory factory) => Manager.RegisterFactory(factory);
        public static void RegisterFocusAdapter(IUIFocusAdapter adapter) => Manager.RegisterFocusAdapter(adapter);
        public static void RegisterResourceProvider(IUIResourceProvider provider) => Manager.ResourceProvider = provider;
        public static void RegisterOverrideApplier(UIRenderBackend backend, UIScreenPropertyOverrideApplier applier)
            => Manager.RegisterOverrideApplier(backend, applier);
    }
}
