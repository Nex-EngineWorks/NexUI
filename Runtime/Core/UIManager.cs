using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Central runtime orchestrator. Owns registries and stacks, selects the right
    /// backend factory, mounts surfaces onto layers, applies policies, drives motion
    /// and lifecycle, and coordinates open / close / toggle / back navigation.
    ///
    /// The manager is entirely backend-agnostic: it only ever touches
    /// <see cref="IUISurface"/>, <see cref="IUIElementHandle"/> and capabilities.
    /// </summary>
    public sealed class UIManager
    {
        private readonly UIScreenRegistry _registry = new UIScreenRegistry();
        private readonly UILayerManager _layers = new UILayerManager();
        private readonly UIFocusManager _focus = new UIFocusManager();
        private readonly UIPolicyRunner _policy = new UIPolicyRunner();
        private readonly UIBackStack _backStack = new UIBackStack();
        private readonly UIModalStack _modalStack = new UIModalStack();
        private readonly UIToastQueue _toastQueue = new UIToastQueue();

        private readonly Dictionary<UIRenderBackend, IUIScreenFactory> _factories =
            new Dictionary<UIRenderBackend, IUIScreenFactory>();
        private readonly Dictionary<string, UIScreenInstance> _open =
            new Dictionary<string, UIScreenInstance>();

        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        private readonly List<IInputPolicy> _inputPolicies = new List<IInputPolicy>();

        public IUIMotionPlayer MotionPlayer { get; set; }
        public IUIMotionResolver MotionResolver { get; set; }

        public UIScreenRegistry Registry => _registry;
        public UILayerManager Layers => _layers;
        public UIFocusManager Focus => _focus;

        // ---- Backend-agnostic event hooks (subscribed by e.g. MessagePipe) --

        /// <summary>Raised after a screen finishes opening.</summary>
        public event System.Action<UIScreenInstance> ScreenOpened;

        /// <summary>Raised after a screen finishes closing.</summary>
        public event System.Action<UIScreenInstance> ScreenClosed;

        /// <summary>
        /// B7 (per-screen fault isolation): raised when a screen's open or close lifecycle
        /// throws. The manager has already rolled the screen back to a closed/removed state by
        /// the time this fires, so the rest of the UI stack keeps working - subscribe to show a
        /// fallback (e.g. an error toast) instead of letting the exception surface only as a
        /// console error.
        /// </summary>
        public event System.Action<string, System.Exception> ScreenFaulted;

        // ---- Debug read-only surface ---------------------------------------

        public IReadOnlyCollection<UIScreenInstance> OpenScreens => _open.Values;
        public IReadOnlyList<string> BackStackSnapshot() => _backStack.Snapshot();
        public IReadOnlyList<string> ModalStackSnapshot() => _modalStack.Snapshot();
        public int ToastQueueCount => _toastQueue.Count;
        public IReadOnlyCollection<UIRenderBackend> RegisteredBackends => _factories.Keys;
        public string LastFocusedElementId { get; private set; }

        // ---- Registration ---------------------------------------------------

        public void RegisterScreen(UIScreenDefinition definition) => _registry.Register(definition);

        public void RegisterFactory(IUIScreenFactory factory)
        {
            if (factory == null) return;
            _factories[factory.Backend] = factory;
        }

        public void RegisterFocusAdapter(IUIFocusAdapter adapter) => _focus.RegisterAdapter(adapter);

        public void RegisterLayer(IUILayerRoot layerRoot) => _layers.RegisterLayer(layerRoot);

        public void RegisterInputPolicy(IInputPolicy policy)
        {
            if (policy != null && !_inputPolicies.Contains(policy))
                _inputPolicies.Add(policy);
        }

        // ---- Queries --------------------------------------------------------

        public bool IsOpen(string screenId) => _open.ContainsKey(screenId);

        public IUISurface GetSurface(string screenId)
            => _open.TryGetValue(screenId, out var inst) ? inst.Surface : null;

        // ---- Open -----------------------------------------------------------

        public async UniTask OpenAsync(string screenId, UIOpenArgs args = default)
        {
            if (!_registry.TryGet(screenId, out var def))
            {
                Debug.LogError($"[NexUI] OpenAsync: unknown screenId '{screenId}'.");
                return;
            }

            var openPolicy = def.layer.openPolicy;

            if (_open.ContainsKey(screenId) && openPolicy == UIOpenPolicy.Single)
            {
                // Already open and single-instance: bring focus and return.
                _focus.Trap(_open[screenId].Surface, def.focus.defaultFocusElementId);
                return;
            }

            if (openPolicy == UIOpenPolicy.ReplaceLayer)
                await CloseLayerExceptAsync(def.layer.layerType, screenId);

            var backend = def.backendAsset.backend;
            if (!_factories.TryGetValue(backend, out var factory))
            {
                Debug.LogError($"[NexUI] No screen factory registered for backend '{backend}' (screen '{screenId}').");
                return;
            }

            var parent = _layers.ResolveParentSurface(backend, def.layer.layerType);
            var surface = await factory.CreateAsync(def, parent, _lifetimeCts.Token);
            if (surface == null)
            {
                Debug.LogError($"[NexUI] Factory for backend '{backend}' returned a null surface for '{screenId}'.");
                return;
            }

            var instance = new UIScreenInstance(def, surface) { State = UIScreenState.Opening };
            _open[screenId] = instance;

            // B7 (per-screen fault isolation): a throw anywhere below (a bad lifecycle hook, a
            // broken motion asset, a misbehaving input policy) previously propagated out of
            // OpenAsync with `instance` already left in `_open` mid-"Opening" - IsOpen(screenId)
            // would then report true forever for a screen that never finished opening, and the
            // exception could take down whatever awaited this call. Roll back to closed instead.
            try
            {
                surface.SetActive(true);
                surface.SetSortingOrder(_layers.ResolveBaseSortingOrder(backend, def.layer.layerType) + def.identity.priority);
                surface.SetInputBlocking(def.policy.blockInputBehind || def.layer.layerType == UILayerType.Modal);

                var ctx = new UIScreenContext(screenId, surface, _lifetimeCts.Token);

                if (instance.Lifecycle != null)
                    await instance.Lifecycle.OnBeforeOpenAsync(ctx);

                _policy.Apply(instance);

                for (int i = 0; i < _inputPolicies.Count; i++)
                    _inputPolicies[i].Apply(def);

                if (def.layer.layerType == UILayerType.Modal)
                    _modalStack.Push(screenId);

                if (def.focus.trapFocus || def.policy.focusPolicy == UIFocusPolicy.TrapFocus)
                {
                    _focus.Trap(surface, def.focus.defaultFocusElementId);
                    LastFocusedElementId = def.focus.defaultFocusElementId;
                }

                if (openPolicy == UIOpenPolicy.StackPush)
                    _backStack.Push(screenId);

                if (!args.suppressMotion)
                    await PlayMotionAsync(surface, def.motion.openMotion);

                instance.State = UIScreenState.Open;

                if (instance.Lifecycle != null)
                    await instance.Lifecycle.OnAfterOpenAsync(ctx);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NexUI] OpenAsync('{screenId}') threw during open - rolling back so the rest of the UI stack keeps working: {ex}");
                _open.Remove(screenId);
                _modalStack.Remove(screenId);
                _backStack.Remove(screenId);
                if (def.focus.trapFocus || def.policy.focusPolicy == UIFocusPolicy.TrapFocus)
                    _focus.Release(surface, false);
                surface.SetActive(false);
                surface.Destroy();
                instance.State = UIScreenState.Closed;
                ScreenFaulted?.Invoke(screenId, ex);
                return;
            }

            ScreenOpened?.Invoke(instance);
        }

        // ---- Close ----------------------------------------------------------

        public async UniTask CloseAsync(string screenId, UICloseArgs args = default)
        {
            if (!_open.TryGetValue(screenId, out var instance))
                return;

            instance.State = UIScreenState.Closing;
            var def = instance.Definition;
            var surface = instance.Surface;
            var ctx = new UIScreenContext(screenId, surface, _lifetimeCts.Token);

            // B7 (per-screen fault isolation): if a close hook throws, still force the screen out
            // of `_open` and destroy its surface in the catch below - otherwise a broken close
            // hook permanently wedges this screenId (IsOpen keeps returning true, a later
            // OpenAsync for the same id would collide with the never-removed entry).
            try
            {
                if (instance.Lifecycle != null)
                    await instance.Lifecycle.OnBeforeCloseAsync(ctx);

                if (!args.suppressMotion && !args.immediate)
                    await PlayMotionAsync(surface, def.motion.closeMotion);

                if (def.focus.trapFocus || def.policy.focusPolicy == UIFocusPolicy.TrapFocus)
                    _focus.Release(surface, def.focus.restoreFocusOnClose);

                _policy.Revert(instance);

                for (int i = 0; i < _inputPolicies.Count; i++)
                    _inputPolicies[i].Release(def);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NexUI] CloseAsync('{screenId}') threw during close - forcing the screen closed anyway: {ex}");
                ScreenFaulted?.Invoke(screenId, ex);
            }

            _modalStack.Remove(screenId);
            _backStack.Remove(screenId);

            surface.SetActive(false);
            surface.Destroy();

            _open.Remove(screenId);
            instance.State = UIScreenState.Closed;

            if (instance.Lifecycle != null)
            {
                try { await instance.Lifecycle.OnAfterCloseAsync(ctx); }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[NexUI] OnAfterCloseAsync('{screenId}') threw: {ex}");
                    ScreenFaulted?.Invoke(screenId, ex);
                }
            }

            ScreenClosed?.Invoke(instance);

            // Drain the toast queue if this was the active toast.
            if (def.layer.layerType == UILayerType.Toast)
            {
                _toastQueue.MarkFinished(screenId);
                await DrainToastQueueAsync();
            }
        }

        // ---- Toggle / Back --------------------------------------------------

        public UniTask ToggleAsync(string screenId)
            => IsOpen(screenId) ? CloseAsync(screenId) : OpenAsync(screenId);

        public async UniTask BackAsync()
        {
            if (_backStack.TryPop(out var screenId) && _open.ContainsKey(screenId))
            {
                await CloseAsync(screenId);
                return;
            }

            // Fallback: close the top-most modal if any.
            if (_modalStack.TryGetTop(out var modalId))
                await CloseAsync(modalId);
        }

        // ---- Internals ------------------------------------------------------

        private async UniTask CloseLayerExceptAsync(UILayerType layer, string keepScreenId)
        {
            var toClose = _open.Values
                .Where(i => i.Layer == layer && i.ScreenId != keepScreenId)
                .Select(i => i.ScreenId)
                .ToList();

            foreach (var id in toClose)
                await CloseAsync(id, new UICloseArgs { immediate = true });
        }

        private async UniTask DrainToastQueueAsync()
        {
            if (_toastQueue.ActiveScreenId != null) return;
            if (_toastQueue.TryDequeue(out var req))
                await OpenAsync(req.screenId, req.args);
        }

        private async UniTask PlayMotionAsync(IUISurface surface, UnityEngine.Object motionAsset)
        {
            if (MotionPlayer == null || MotionResolver == null || motionAsset == null || surface?.RootHandle == null)
                return;

            var timeline = MotionResolver.Resolve(motionAsset);
            if (timeline == null || timeline == UIMotionTimeline.Empty)
                return;

            await MotionPlayer.PlayAsync(surface.RootHandle, timeline, _lifetimeCts.Token);
        }

        public void Shutdown()
        {
            _lifetimeCts.Cancel();
            foreach (var inst in _open.Values.ToList())
                inst.Surface?.Destroy();
            _open.Clear();
            _backStack.Clear();
            _modalStack.Clear();
            _toastQueue.Clear();
            _policy.Reset();
        }
    }
}
