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

        /// <summary>
        /// Shuts the current manager down (closing screens, cancelling transitions) and drops it, so
        /// the next access builds a fresh one. Required between domain-reload-free play sessions and
        /// after tests - statics otherwise carry screens across runs.
        /// </summary>
        public static void Reset()
        {
            var old = _manager;
            _manager = null;
            old?.Shutdown();
        }

        // ---- Async API ------------------------------------------------------

        public static Task OpenAsync(string screenId, UIOpenArgs args = default,
            System.Threading.CancellationToken cancellationToken = default)
            => Manager.OpenAsync(screenId, args, cancellationToken);

        public static Task CloseAsync(string screenId, UICloseArgs args = default,
            System.Threading.CancellationToken cancellationToken = default)
            => Manager.CloseAsync(screenId, args, cancellationToken);

        public static Task ToggleAsync(string screenId) => Manager.ToggleAsync(screenId);
        public static Task BackAsync() => Manager.BackAsync();
        public static Task BackAsync<TResult>(TResult result) => Manager.BackAsync(result);

        /// <summary>Closes every open screen EXCEPT the named one.</summary>
        public static Task CloseOthersAsync(string keepScreenId, UICloseArgs args = default,
            System.Threading.CancellationToken cancellationToken = default)
            => Manager.CloseOthersAsync(keepScreenId, args, cancellationToken);
        public static Task PreloadAsync() => Manager.PreloadAsync();
        public static Task PreloadAsync(string screenId) => Manager.PreloadAsync(screenId);
        public static Task CloseAllAsync(UICloseArgs args = default,
            System.Threading.CancellationToken cancellationToken = default)
            => Manager.CloseAllAsync(args, cancellationToken);

        public static Task CloseLayerAsync(UILayerType layer, UICloseArgs args = default,
            System.Threading.CancellationToken cancellationToken = default)
            => Manager.CloseLayerAsync(layer, args, cancellationToken);

        /// <summary>Completes when the screen next closes, with the closer's <see cref="UICloseArgs.result"/>.</summary>
        public static Task<object> WaitForCloseAsync(string screenId,
            System.Threading.CancellationToken cancellationToken = default)
            => Manager.WaitForCloseAsync(screenId, cancellationToken);

        public static UIScreenStackSnapshot CaptureStackSnapshot() => Manager.CaptureStackSnapshot();
        public static Task RestoreStackAsync(UIScreenStackSnapshot snapshot,
            System.Threading.CancellationToken cancellationToken = default)
            => Manager.RestoreStackAsync(snapshot, cancellationToken);

        // ---- Fire-and-forget sync API --------------------------------------

        public static void Open(string screenId) => _ = Manager.OpenAsync(screenId);
        public static void Close(string screenId) => _ = Manager.CloseAsync(screenId);
        public static void Toggle(string screenId) => _ = Manager.ToggleAsync(screenId);
        public static void Back() => _ = Manager.BackAsync();

        // ---- Queries / registration ----------------------------------------

        public static bool IsOpen(string screenId) => Manager.IsOpen(screenId);

        public static void RegisterScreen(UIScreenDefinition definition) => Manager.RegisterScreen(definition);
        public static void UnregisterScreen(string screenId) => Manager.UnregisterScreen(screenId);
        public static void RegisterFactory(IUIScreenFactory factory) => Manager.RegisterFactory(factory);
        public static void RegisterFocusAdapter(IUIFocusAdapter adapter) => Manager.RegisterFocusAdapter(adapter);
        public static void RegisterResourceProvider(IUIResourceProvider provider) => Manager.ResourceProvider = provider;
        public static void RegisterOverrideApplier(UIRenderBackend backend, UIScreenPropertyOverrideApplier applier)
            => Manager.RegisterOverrideApplier(backend, applier);
    }
}
