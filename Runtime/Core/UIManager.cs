using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Core
{
    /// <summary>Backend extension point for variant/responsive property paths Core does not know.</summary>
    public delegate bool UIScreenPropertyOverrideApplier(
        IUISurface surface, string elementId, string propertyPath, string value);

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

        /// <summary>Last result each closed screen handed back via <see cref="UICloseArgs.result"/>.</summary>
        private readonly Dictionary<string, object> _closeResults =
            new Dictionary<string, object>();

        /// <summary>Waiters registered by <see cref="WaitForCloseAsync"/>, keyed by screen id.</summary>
        private readonly Dictionary<string, List<TaskCompletionSource<object>>> _closeWaiters =
            new Dictionary<string, List<TaskCompletionSource<object>>>();

        private readonly Dictionary<UIRenderBackend, IUIScreenFactory> _factories =
            new Dictionary<UIRenderBackend, IUIScreenFactory>();
        private readonly Dictionary<string, UIScreenInstance> _open =
            new Dictionary<string, UIScreenInstance>();
        private readonly Dictionary<string, UIScreenInstance> _retained =
            new Dictionary<string, UIScreenInstance>();
        private readonly Dictionary<string, TransitionHandle> _transitions =
            new Dictionary<string, TransitionHandle>();

        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        private readonly List<IInputPolicy> _inputPolicies = new List<IInputPolicy>();
        private readonly Dictionary<UIRenderBackend, UIScreenPropertyOverrideApplier> _overrideAppliers =
            new Dictionary<UIRenderBackend, UIScreenPropertyOverrideApplier>();

        /// <summary>
        /// Every live manager registers itself once, so theme changes can find open surfaces without
        /// Core referencing the Theme module. Instances remove themselves in <see cref="Shutdown"/>.
        /// </summary>
        private static readonly List<UIManager> _liveManagers = new List<UIManager>();

        static UIManager()
        {
            Abstractions.UIOpenSurfaceRegistry.RegisterProvider(CollectLiveSurfaces);
        }

        private static IEnumerable<Abstractions.IUISurface> CollectLiveSurfaces()
        {
            // Snapshot: Shutdown during enumeration must not mutate the walked list.
            for (var i = 0; i < _liveManagers.Count; i++)
            {
                var manager = _liveManagers[i];
                if (manager == null) continue;
                foreach (var instance in manager.OpenScreens)
                    if (instance?.Surface != null) yield return instance.Surface;
            }
        }

        public UIManager()
        {
            _liveManagers.Add(this);
        }

        private sealed class TransitionHandle
        {
            public readonly CancellationTokenSource Cancellation;

            public readonly TaskCompletionSource<bool> Completion =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public TransitionHandle(CancellationToken lifetimeToken)
            {
                Cancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        lifetimeToken);
            }

            /// <summary>
            /// Links the caller's token in as well, so cancelling OpenAsync/CloseAsync rolls the
            /// operation back. Disposing the linked source also releases the registration on the
            /// caller's token.
            /// </summary>
            public TransitionHandle(CancellationToken lifetimeToken, CancellationToken externalToken)
            {
                Cancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        lifetimeToken, externalToken);
            }
        }

        public IUIMotionPlayer MotionPlayer { get; set; }
        public IUIMotionResolver MotionResolver { get; set; }
        public IUIResourceProvider ResourceProvider { get; set; }
        public UIInputMode InputMode { get; set; } = UIInputMode.KeyboardMouse;
        public System.Func<Vector2Int> ResolutionProvider { get; set; } =
            () => new Vector2Int(Screen.width, Screen.height);

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

        /// <summary>Removes a registration; open instances are unaffected.</summary>
        public void UnregisterScreen(string screenId) => _registry.Unregister(screenId);

        public void RegisterFactory(IUIScreenFactory factory)
        {
            if (factory == null) return;
            _factories[factory.Backend] = factory;
        }

        public void RegisterFocusAdapter(IUIFocusAdapter adapter) => _focus.RegisterAdapter(adapter);

        public void RegisterLayer(IUILayerRoot layerRoot) => _layers.RegisterLayer(layerRoot);

        /// <summary>Removes a previously registered layer root (e.g. when its bootstrap is destroyed).</summary>
        public void UnregisterLayer(IUILayerRoot layerRoot) => _layers.UnregisterLayer(layerRoot);

        public void RegisterInputPolicy(IInputPolicy policy)
        {
            if (policy != null && !_inputPolicies.Contains(policy))
                _inputPolicies.Add(policy);
        }

        public void RegisterOverrideApplier(UIRenderBackend backend, UIScreenPropertyOverrideApplier applier)
        {
            if (applier == null) _overrideAppliers.Remove(backend);
            else _overrideAppliers[backend] = applier;
        }

        // ---- Queries --------------------------------------------------------

        public bool IsOpen(string screenId) => _open.ContainsKey(screenId);

        public IUISurface GetSurface(string screenId)
            => _open.TryGetValue(screenId, out var inst) ? inst.Surface : null;

        /// <summary>Creates and retains every screen configured for startup preloading.</summary>
        public async Task PreloadAsync()
        {
            var ids = _registry.Definitions.Values
                .Where(def => def != null && def.loadStrategy == UIScreenLoadStrategy.Preload)
                .Select(def => def.ScreenId)
                .ToList();
            foreach (var id in ids)
                await PreloadAsync(id);
        }

        /// <summary>Creates one inactive surface now so its first OpenAsync does not instantiate.</summary>
        public Task PreloadAsync(string screenId)
            => PreloadInternalAsync(screenId, CancellationToken.None);

        /// <summary>
        /// Creates one inactive surface now so its first OpenAsync does not instantiate.
        /// Cancelling <paramref name="cancellationToken"/> rolls the preload back.
        /// </summary>
        public Task PreloadAsync(string screenId, CancellationToken cancellationToken)
            => PreloadInternalAsync(screenId, cancellationToken);

        private async Task PreloadInternalAsync(string screenId, CancellationToken cancellationToken)
        {
            if (!_registry.TryGet(screenId, out var def))
            {
                Debug.LogError($"[NexUI] PreloadAsync: unknown screenId '{screenId}'.");
                return;
            }

            var transition = await BeginTransitionAsync(screenId, def.policy.conflictPolicy, cancellationToken);
            if (transition == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }
            IUISurface surface = null;
            try
            {
                if (_open.ContainsKey(screenId) || _retained.ContainsKey(screenId)) return;
                if (!_factories.TryGetValue(def.backendAsset.backend, out var factory))
                    throw new System.InvalidOperationException(
                        $"No screen factory registered for backend '{def.backendAsset.backend}' (screen '{screenId}').");
                var parent = ResolveMountParent(def);
                surface = await CreateSurfaceAsync(def, parent, factory, transition.Cancellation.Token);
                transition.Cancellation.Token.ThrowIfCancellationRequested();
                if (surface == null)
                    throw new System.InvalidOperationException(
                        $"Factory for backend '{def.backendAsset.backend}' returned a null surface for '{screenId}'.");
                surface.SetActive(false);
                _retained[screenId] = new UIScreenInstance(def, surface) { State = UIScreenState.Closed };
                surface = null;
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SafeDestroySurface(screenId, surface);
                throw;
            }
            catch (System.OperationCanceledException)
            {
                SafeDestroySurface(screenId, surface);
            }
            catch (System.Exception ex)
            {
                SafeDestroySurface(screenId, surface);
                Debug.LogError($"[NexUI] PreloadAsync('{screenId}') failed: {ex}");
                RaiseScreenFaulted(screenId, ex);
            }
            finally
            {
                EndTransition(screenId, transition);
            }
        }

        // ---- Open -----------------------------------------------------------

        public Task OpenAsync(string screenId, UIOpenArgs args = default,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(args.variantId) || args.payload != null)
                _lastArgsByScreen[screenId] = args;
            return OpenInternalAsync(screenId, args, false, new HashSet<string>(), cancellationToken);
        }

        private async Task OpenInternalAsync(string screenId, UIOpenArgs args, bool fromToastQueue,
            HashSet<string> relationChain, CancellationToken cancellationToken = default)
        {
            relationChain ??= new HashSet<string>();
            if (!relationChain.Add(screenId)) return;
            if (!_registry.TryGet(screenId, out var def))
            {
                Debug.LogError($"[NexUI] OpenAsync: unknown screenId '{screenId}'.");
                return;
            }

            if (!string.IsNullOrEmpty(def.relations.parentScreenId) && def.relations.parentScreenId != screenId)
                await OpenInternalAsync(def.relations.parentScreenId, default, false, relationChain);

            var openPolicy = def.layer.openPolicy;
            var ownsToastSlot = false;
            if (openPolicy == UIOpenPolicy.Queue)
            {
                if (!fromToastQueue)
                {
                    if (!_toastQueue.TryActivate(screenId))
                    {
                        _toastQueue.Enqueue(screenId, args);
                        return;
                    }
                }
                ownsToastSlot = _toastQueue.ActiveScreenId == screenId;
            }

            var transition = await BeginTransitionAsync(screenId, def.policy.conflictPolicy, cancellationToken);
            if (transition == null)
            {
                // Conflict policy dropped this request. Release the toast slot, otherwise the
                // queue keeps ActiveScreenId set forever and DrainToastQueueAsync never fires.
                if (ownsToastSlot)
                {
                    _toastQueue.MarkFinished(screenId);
                    await DrainToastQueueAsync();
                }
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            IUISurface surface = null;
            UIScreenInstance instance = null;
            var policyApplied = false;
            var appliedInputPolicies = 0;
            var focusTrapped = false;
            var opened = false;
            var newlyOpened = false;
            var fromRetained = false;
            try
            {
                // Screen ids are unique manager keys. Additive means coexistence with other
                // screen ids on the layer, not a second untracked copy of this same id.
                if (_open.TryGetValue(screenId, out var existing))
                {
                    _focus.Trap(existing.Surface, def.focus.defaultFocusElementId);
                    opened = true;
                    return;
                }

                var token = transition.Cancellation.Token;
                var backend = def.backendAsset.backend;
                if (_retained.TryGetValue(screenId, out instance))
                {
                    _retained.Remove(screenId);
                    surface = instance.Surface;
                    fromRetained = true;
                }
                else
                {
                    if (!_factories.TryGetValue(backend, out var factory))
                        throw new System.InvalidOperationException(
                            $"No screen factory registered for backend '{backend}' (screen '{screenId}').");

                    var parent = ResolveMountParent(def);
                    surface = await CreateSurfaceAsync(def, parent, factory, token);
                    token.ThrowIfCancellationRequested();
                    if (surface == null)
                        throw new System.InvalidOperationException(
                            $"Factory for backend '{backend}' returned a null surface for '{screenId}'.");
                }

                // Prevent a newly-created surface flashing above the old layer contents while a
                // ReplaceLayer close transition runs. Sibling closes START here (so their exit
                // motion overlaps this screen's open motion = crossfade) and are awaited just
                // before the open finishes, so state settles deterministically.
                surface.SetActive(false);
                Task closingSiblings = null;
                if (openPolicy == UIOpenPolicy.ReplaceLayer)
                    closingSiblings = CloseLayerExceptAsync(def.layer.layerType, screenId, immediate: false);
                token.ThrowIfCancellationRequested();

                if (def.relations.closes != null)
                    for (int i = 0; i < def.relations.closes.Length; i++)
                    {
                        var relatedId = def.relations.closes[i];
                        if (!string.IsNullOrEmpty(relatedId) && relatedId != screenId)
                            await CloseAsync(relatedId, new UICloseArgs { immediate = true });
                    }
                token.ThrowIfCancellationRequested();

                instance ??= new UIScreenInstance(def, surface);
                instance.State = UIScreenState.Opening;
                _open[screenId] = instance;

                surface.SetActive(true);
                surface.SetSortingOrder(_layers.ResolveBaseSortingOrder(backend, def.layer.layerType) + def.identity.priority);
                surface.SetInputBlocking(def.policy.blockInputBehind || def.layer.layerType == UILayerType.Modal);
                ApplyAuthoredOverrides(def, surface, args);

                var ctx = new UIScreenContext(screenId, surface, token);

                if (instance.Lifecycle != null)
                    await instance.Lifecycle.OnBeforeOpenAsync(ctx);
                token.ThrowIfCancellationRequested();

                _policy.Apply(instance);
                policyApplied = true;

                for (int i = 0; i < _inputPolicies.Count; i++)
                {
                    _inputPolicies[i].Apply(def);
                    appliedInputPolicies++;
                }

                if (def.layer.layerType == UILayerType.Modal)
                    _modalStack.Push(screenId);

                if (def.focus.trapFocus || def.policy.focusPolicy != UIFocusPolicy.None)
                {
                    _focus.Trap(surface, def.focus.defaultFocusElementId);
                    LastFocusedElementId = def.focus.defaultFocusElementId;
                    focusTrapped = true;
                }

                if (openPolicy == UIOpenPolicy.StackPush)
                    _backStack.Push(screenId);

                if (!args.suppressMotion)
                    await PlayMotionAsync(surface, def.motion.openMotion, token);
                token.ThrowIfCancellationRequested();

                // The crossfade handoff: sibling exits ran alongside our open motion. Wait them
                // out so the layer reaches a settled state before OnAfterOpen fires.
                if (closingSiblings != null)
                {
                    try { await closingSiblings; }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[NexUI] ReplaceLayer sibling close failed: {ex}");
                    }
                }
                token.ThrowIfCancellationRequested();

                instance.State = UIScreenState.Open;

                if (instance.Lifecycle != null)
                    await instance.Lifecycle.OnAfterOpenAsync(ctx);
                token.ThrowIfCancellationRequested();
                opened = true;
                newlyOpened = true;
            }
            catch (System.OperationCanceledException)
            {
                RollbackOpen(screenId, instance, surface, policyApplied, appliedInputPolicies, focusTrapped, fromRetained);
                // A conflict-policy Cancel also lands here; only the caller's own token turns into
                // an observable cancelled task.
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NexUI] OpenAsync('{screenId}') threw during open - rolling back so the rest of the UI stack keeps working: {ex}");
                RollbackOpen(screenId, instance, surface, policyApplied, appliedInputPolicies, focusTrapped, fromRetained);
                RaiseScreenFaulted(screenId, ex);
            }
            finally
            {
                EndTransition(screenId, transition);
            }

            if (opened && instance != null)
            {
                RaiseScreenOpened(instance);
                if (newlyOpened && def.relations.opensWith != null)
                    for (int i = 0; i < def.relations.opensWith.Length; i++)
                    {
                        var relatedId = def.relations.opensWith[i];
                        if (!string.IsNullOrEmpty(relatedId) && relatedId != screenId)
                            await OpenInternalAsync(relatedId, default, false, relationChain);
                    }
            }
            else if (ownsToastSlot)
            {
                _toastQueue.MarkFinished(screenId);
                await DrainToastQueueAsync();
            }
        }

        // ---- Close ----------------------------------------------------------

        public Task CloseAsync(string screenId, UICloseArgs args = default,
            CancellationToken cancellationToken = default)
            => CloseInternalAsync(screenId, args, cancellationToken);

        private async Task CloseInternalAsync(string screenId, UICloseArgs args,
            CancellationToken cancellationToken)
        {
            if (!_registry.TryGet(screenId, out var registeredDef) && !_open.ContainsKey(screenId))
                return;

            var conflictPolicy = _open.TryGetValue(screenId, out var current)
                ? current.Definition.policy.conflictPolicy
                : registeredDef.policy.conflictPolicy;
            var transition = await BeginTransitionAsync(screenId, conflictPolicy, cancellationToken);
            if (transition == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            UIScreenInstance instance = null;
            UIScreenDefinition def = null;
            var wasToast = false;
            try
            {
                try
                {
                    if (!_open.TryGetValue(screenId, out instance)) return;

                    instance.State = UIScreenState.Closing;
                    def = instance.Definition;
                    wasToast = def.layer.layerType == UILayerType.Toast;
                    var surface = instance.Surface;
                    var token = transition.Cancellation.Token;
                    var ctx = new UIScreenContext(screenId, surface, token);

                    if (instance.Lifecycle != null)
                        await instance.Lifecycle.OnBeforeCloseAsync(ctx);

                    if (!args.suppressMotion && !args.immediate)
                        await PlayMotionAsync(surface, def.motion.closeMotion, token);
                }
                catch (System.OperationCanceledException)
                {
                    // A Cancel conflict still completes the close cleanup below so the replacement
                    // operation starts from a deterministic closed state.
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[NexUI] CloseAsync('{screenId}') threw during close - forcing the screen closed anyway: {ex}");
                    RaiseScreenFaulted(screenId, ex);
                }

                if (instance != null)
                {
                    var surface = instance.Surface;
                    try
                    {
                        if (def.focus.trapFocus || def.policy.focusPolicy != UIFocusPolicy.None)
                            _focus.Release(surface, def.focus.restoreFocusOnClose);
                    }
                    catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }

                    try { _policy.Revert(instance); }
                    catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }

                    for (int i = 0; i < _inputPolicies.Count; i++)
                        try { _inputPolicies[i].Release(def); }
                        catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }

                    _modalStack.Remove(screenId);
                    _backStack.Remove(screenId);

                    if (ShouldRetain(def))
                    {
                        try { surface.SetActive(false); }
                        catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }
                        _retained[screenId] = instance;
                    }
                    else
                    {
                        SafeDestroySurface(screenId, surface);
                    }

                    _open.Remove(screenId);
                    instance.State = UIScreenState.Closed;

                    if (instance.Lifecycle != null)
                    {
                        var ctx = new UIScreenContext(screenId, surface, transition.Cancellation.Token);
                        try { await instance.Lifecycle.OnAfterCloseAsync(ctx); }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[NexUI] OnAfterCloseAsync('{screenId}') threw: {ex}");
                            RaiseScreenFaulted(screenId, ex);
                        }
                    }
                }
            }
            finally
            {
                EndTransition(screenId, transition);
            }

            if (instance != null)
            {
                CompleteCloseWaiters(screenId, args.result);
                RaiseScreenClosed(instance);
            }
            if (wasToast)
            {
                _toastQueue.MarkFinished(screenId);
                await DrainToastQueueAsync();
            }
        }

        // ---- Bulk close -----------------------------------------------------

        /// <summary>Closes every open screen. Snapshot-first, so closing does not walk a mutating set.</summary>
        public Task CloseAllAsync(UICloseArgs args = default, CancellationToken cancellationToken = default)
            => CloseManyAsync(_open.Keys.ToArray(), args, cancellationToken);

        /// <summary>Closes every open screen on one layer - "return to lobby", "close all popups".</summary>
        public Task CloseLayerAsync(UILayerType layer, UICloseArgs args = default, CancellationToken cancellationToken = default)
        {
            List<string> ids = null;
            foreach (var pair in _open)
            {
                if (pair.Value.Layer != layer) continue;
                ids ??= new List<string>();
                ids.Add(pair.Key);
            }
            return ids == null ? Task.CompletedTask : CloseManyAsync(ids, args, cancellationToken);
        }

        private async Task CloseManyAsync(IReadOnlyList<string> screenIds, UICloseArgs args, CancellationToken ct)
        {
            for (int i = 0; i < screenIds.Count; i++)
            {
                if (!_open.ContainsKey(screenIds[i])) continue;   // closed by an earlier relation
                await CloseAsync(screenIds[i], args, ct);
            }
        }

        // ---- Close result waiting -------------------------------------------

        /// <summary>
        /// Completes when the named screen next closes, handing back the <see cref="UICloseArgs.result"/>
        /// its closer supplied. The request/response half of dialog navigation:
        /// <c>var picked = await ui.WaitForCloseAsync("ItemPicker");</c>
        ///
        /// Calling this for an ALREADY closed screen completes immediately with that screen's last
        /// recorded result (or null), so fire-and-forget opens followed by an await never deadlock.
        /// </summary>
        public Task<object> WaitForCloseAsync(string screenId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(screenId)) return Task.FromResult<object>(null);
            if (!_open.ContainsKey(screenId))
                return Task.FromResult<object>(
                    _closeResults.TryGetValue(screenId, out var last) ? last : null);

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_closeWaiters.TryGetValue(screenId, out var list))
            {
                list = new List<TaskCompletionSource<object>>();
                _closeWaiters[screenId] = list;
            }
            list.Add(tcs);

            if (!cancellationToken.CanBeCanceled) return tcs.Task;

            var reg = cancellationToken.Register(() =>
            {
                if (list.Remove(tcs)) tcs.TrySetCanceled(cancellationToken);
            });
            return tcs.Task.ContinueWith(t =>
            {
                reg.Dispose();
                return t;
            }).Unwrap();
        }

        private void CompleteCloseWaiters(string screenId, object result)
        {
            _closeResults[screenId] = result;
            if (_closeWaiters.TryGetValue(screenId, out var waiters))
            {
                _closeWaiters.Remove(screenId);
                foreach (var waiter in waiters)
                    waiter.TrySetResult(result);
            }
        }

        // ---- Toggle / Back --------------------------------------------------

        public Task ToggleAsync(string screenId)
            => IsOpen(screenId) ? CloseAsync(screenId) : OpenAsync(screenId);

        /// <summary>Pops the back stack and closes the screen it lands on.</summary>
        public Task BackAsync() => BackAsync<object>(null);

        /// <summary>
        /// Pops the back stack and closes the screen it lands on, handing <paramref name="result"/>
        /// to anyone awaiting <see cref="WaitForCloseAsync"/> - the back gesture can carry a result
        /// just like an explicit close.
        /// </summary>
        public async Task BackAsync<TResult>(TResult result)
        {
            while (_backStack.TryPop(out var screenId))
            {
                if (_open.TryGetValue(screenId, out var stacked) &&
                    (stacked.Definition.policy.closeOnBack || stacked.Definition.layer.openPolicy == UIOpenPolicy.StackPush))
                {
                    await CloseAsync(screenId, new UICloseArgs { result = result });
                    return;
                }
            }

            // Fallback: close the top-most modal if any.
            if (_modalStack.TryGetTop(out var modalId) && _open.TryGetValue(modalId, out var modal) &&
                modal.Definition.policy.closeOnBack)
                await CloseAsync(modalId, new UICloseArgs { result = result });
        }

        /// <summary>
        /// Closes every open screen EXCEPT <paramref name="keepScreenId"/> - "focus mode" for one
        /// panel across all layers. The kept screen stays exactly as it is.
        /// </summary>
        public Task CloseOthersAsync(string keepScreenId, UICloseArgs args = default,
            CancellationToken cancellationToken = default)
        {
            List<string> ids = null;
            foreach (var pair in _open)
            {
                if (pair.Key == keepScreenId) continue;
                ids ??= new List<string>();
                ids.Add(pair.Key);
            }
            return ids == null ? Task.CompletedTask : CloseManyAsync(ids, args, cancellationToken);
        }

        // ---- Stack snapshot / restore ---------------------------------------

        /// <summary>
        /// Captures every open screen (bottom ??top by layer, then id) with the open args it was
        /// last given. Pair with <see cref="RestoreStackAsync"/> for "quit and continue later".
        /// </summary>
        public UIScreenStackSnapshot CaptureStackSnapshot()
        {
            var snapshot = new UIScreenStackSnapshot();
            var ordered = new List<UIScreenInstance>(_open.Values);
            ordered.Sort((a, b) =>
            {
                var layer = ((int)a.Layer).CompareTo((int)b.Layer);
                return layer != 0 ? layer : string.CompareOrdinal(a.ScreenId, b.ScreenId);
            });
            foreach (var inst in ordered)
            {
                if (inst == null) continue;
                snapshot.Entries.Add(new UIScreenStackSnapshot.Entry
                {
                    ScreenId = inst.ScreenId,
                    Args = _lastArgsByScreen.TryGetValue(inst.ScreenId, out var args) ? args : default
                });
            }
            return snapshot;
        }

        /// <summary>Reopens the captured set: closes everything first, then opens in capture order.</summary>
        public async Task RestoreStackAsync(UIScreenStackSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            if (snapshot == null) return;
            await CloseAllAsync(new UICloseArgs { suppressMotion = true }, cancellationToken);

            foreach (var entry in snapshot.Entries)
            {
                if (!_registry.Contains(entry.ScreenId))
                {
                    Debug.LogWarning($"[NexUI] RestoreStack: screen '{entry.ScreenId}' is no longer registered; skipped.");
                    continue;
                }
                await OpenAsync(entry.ScreenId, entry.Args, cancellationToken);
            }
        }


        private readonly Dictionary<string, UIOpenArgs> _lastArgsByScreen =
            new Dictionary<string, UIOpenArgs>();

        // ---- Internals ------------------------------------------------------

        private async Task CloseLayerExceptAsync(UILayerType layer, string keepScreenId, bool immediate)
        {
            // Allocation-light: a plain pass instead of LINQ on every ReplaceLayer open.
            List<string> toClose = null;
            foreach (var pair in _open)
            {
                if (pair.Value.Layer != layer || pair.Key == keepScreenId) continue;
                toClose ??= new List<string>();
                toClose.Add(pair.Key);
            }
            if (toClose == null) return;

            foreach (var id in toClose)
                await CloseAsync(id, new UICloseArgs { immediate = immediate });
        }

        private async Task DrainToastQueueAsync()
        {
            if (_toastQueue.ActiveScreenId != null) return;
            if (_toastQueue.TryDequeue(out var req))
                await OpenInternalAsync(req.screenId, req.args, true, new HashSet<string>());
        }

        private async Task PlayMotionAsync(IUISurface surface, UnityEngine.Object motionAsset, CancellationToken token)
        {
            if (MotionPlayer == null || MotionResolver == null || motionAsset == null || surface?.RootHandle == null)
                return;

            var timeline = MotionResolver.Resolve(motionAsset);
            if (timeline == null || timeline == UIMotionTimeline.Empty)
                return;

            await MotionPlayer.PlayAsync(surface.RootHandle, timeline, token);
        }

        private async Task<IUISurface> CreateSurfaceAsync(
            UIScreenDefinition definition, IUISurface parent, IUIScreenFactory factory, CancellationToken token)
        {
            if (definition.loadStrategy != UIScreenLoadStrategy.Addressable)
                return await factory.CreateAsync(definition, parent, token);
            if (ResourceProvider == null)
                throw new System.InvalidOperationException(
                    $"Screen '{definition.ScreenId}' uses Addressable loading but no IUIResourceProvider is registered.");
            var key = definition.backendAsset.resourceKey;
            if (string.IsNullOrEmpty(key))
                throw new System.InvalidOperationException(
                    $"Screen '{definition.ScreenId}' uses Addressable loading but has no resourceKey.");

            UIScreenDefinition runtimeDefinition = null;
            var ownsLoad = false;
            try
            {
                var asset = await ResourceProvider.LoadAssetAsync<UnityEngine.Object>(key, token);
                token.ThrowIfCancellationRequested();
                if (asset == null)
                    throw new System.InvalidOperationException(
                        $"Resource provider returned null for screen '{definition.ScreenId}' key '{key}'.");
                // Contract: a non-null result hands this call exactly one reference which the
                // wrapper surface releases on Destroy (or here, if creation fails below).
                ownsLoad = true;
                runtimeDefinition = UnityEngine.Object.Instantiate(definition);
                var backendAsset = runtimeDefinition.backendAsset;
                backendAsset.asset = asset;
                runtimeDefinition.backendAsset = backendAsset;
                var created = await factory.CreateAsync(runtimeDefinition, parent, token);

                // Ownership: the provider handle stays alive for as long as the surface lives.
                // Releasing here would unload shared textures/meshes out from under the
                // instantiated screen; the wrapper releases when the surface is destroyed.
                return new ResourceOwnedSurface(created, ResourceProvider, key);
            }
            catch
            {
                // No surface took ownership of the load (factory threw or open was cancelled):
                // release immediately so a failed open does not leak the addressable.
                if (ownsLoad)
                    ResourceProvider.Release(key);
                throw;
            }
            finally
            {
                if (runtimeDefinition != null) UnityEngine.Object.Destroy(runtimeDefinition);
            }
        }

        /// <summary>
        /// Delegating surface that releases its resource-provider key exactly once, when the
        /// underlying surface is destroyed. Keeps backend code untouched.
        /// </summary>
        private sealed class ResourceOwnedSurface : IUISurface
        {
            private readonly IUISurface _inner;
            private readonly IUIResourceProvider _provider;
            private readonly string _key;
            private bool _released;

            public ResourceOwnedSurface(IUISurface inner, IUIResourceProvider provider, string key)
            {
                _inner = inner;
                _provider = provider;
                _key = key;
            }

            public string ScreenId => _inner.ScreenId;
            public UIRenderBackend Backend => _inner.Backend;
            public object NativeRoot => _inner.NativeRoot;
            public IUIElementHandle RootHandle => _inner.RootHandle;

            public IUIElementHandle TryFind(string elementId) => _inner.TryFind(elementId);
            public IUIElementHandle FindRequired(string elementId) => _inner.FindRequired(elementId);
            public void SetActive(bool active) => _inner.SetActive(active);
            public void SetSortingOrder(int order) => _inner.SetSortingOrder(order);
            public void SetInputBlocking(bool blocking) => _inner.SetInputBlocking(blocking);

            public void Destroy()
            {
                try { _inner.Destroy(); }
                finally
                {
                    if (!_released)
                    {
                        _released = true;
                        _provider.Release(_key);
                    }
                }
            }
        }

        private IUISurface ResolveMountParent(UIScreenDefinition definition)
        {
            var parentId = definition.relations.parentScreenId;
            if (!string.IsNullOrEmpty(parentId) && _open.TryGetValue(parentId, out var parentInstance) &&
                parentInstance.Surface.Backend == definition.backendAsset.backend)
                return parentInstance.Surface;
            return _layers.ResolveParentSurface(definition.backendAsset.backend, definition.layer.layerType);
        }

        private void ApplyAuthoredOverrides(UIScreenDefinition definition, IUISurface surface, UIOpenArgs args)
        {
            if (!string.IsNullOrEmpty(args.variantId))
            {
                UIScreenVariant selected = null;
                if (definition.variants != null)
                    for (int i = 0; i < definition.variants.Length; i++)
                        if (definition.variants[i] != null && definition.variants[i].variantId == args.variantId)
                        {
                            selected = definition.variants[i];
                            break;
                        }
                if (selected == null)
                    Debug.LogError($"[NexUI] Screen '{definition.ScreenId}' has no variant '{args.variantId}'.");
                else if (selected.overrides != null)
                    for (int i = 0; i < selected.overrides.Length; i++)
                    {
                        var item = selected.overrides[i];
                        if (item != null)
                            ApplyPropertyOverride(surface, item.targetElementId, item.propertyPath, item.value);
                    }
            }

            var resolution = ResolutionProvider?.Invoke() ?? new Vector2Int(Screen.width, Screen.height);
            if (definition.responsiveRules == null) return;
            for (int i = 0; i < definition.responsiveRules.Length; i++)
            {
                var rule = definition.responsiveRules[i];
                if (rule == null || resolution.x < rule.minResolution.x || resolution.x > rule.maxResolution.x ||
                    resolution.y < rule.minResolution.y || resolution.y > rule.maxResolution.y ||
                    (rule.constrainInputMode && rule.inputMode != InputMode))
                    continue;
                if (rule.overrides == null) continue;
                for (int j = 0; j < rule.overrides.Count; j++)
                {
                    var item = rule.overrides[j];
                    if (item != null)
                        ApplyPropertyOverride(surface, item.elementId, item.propertyPath, item.value);
                }
            }
        }

        private void ApplyPropertyOverride(IUISurface surface, string elementId, string propertyPath, string value)
        {
            if (surface == null || string.IsNullOrEmpty(elementId) || string.IsNullOrEmpty(propertyPath)) return;
            if (_overrideAppliers.TryGetValue(surface.Backend, out var custom) &&
                custom(surface, elementId, propertyPath, value))
                return;
            var element = surface.TryFind(elementId);
            if (element == null || !TryApplyCommonOverride(element, propertyPath, value))
                Debug.LogWarning($"[NexUI] Could not apply override '{elementId}.{propertyPath}' on screen '{surface.ScreenId}'.");
        }

        private static bool TryApplyCommonOverride(IUIElementHandle element, string propertyPath, string value)
        {
            var normalizedPath = propertyPath.Trim();
            var path = normalizedPath.ToLowerInvariant();
            if (path == "text" && element.As<IUITextCapability>() is { } text)
            {
                text.Text = value;
                return true;
            }
            if ((path == "visible" || path == "visibility" || path == "runtimevisible") &&
                bool.TryParse(value, out var visible) && element.As<IUIVisibilityCapability>() is { } visibility)
            {
                visibility.Visible = visible;
                return true;
            }
            if ((path == "position" || path == "scale") && TryParseVector2(value, out var vector) &&
                element.As<IUITransformCapability>() is { } vectorTransform)
            {
                if (path == "position") vectorTransform.Position = vector;
                else vectorTransform.Scale = new Vector3(vector.x, vector.y, vectorTransform.Scale.z);
                return true;
            }
            if ((path == "backgroundcolor" || path == "tint" || path == "textcolor") &&
                ColorUtility.TryParseHtmlString(value != null && value.StartsWith("#") ? value : "#" + value, out var color) &&
                element.As<IUIColorCapability>() is { } colors)
            {
                if (path == "textcolor") colors.TextColor = color;
                else colors.BackgroundColor = color;
                return true;
            }
            if ((path == "interactable" || path == "enabled") &&
                bool.TryParse(value, out var interactable) && element.As<IUIInteractableCapability>() is { } interaction)
            {
                interaction.Interactable = interactable;
                return true;
            }
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                if (element.As<IUIValueCapability>() is { } scalar)
                {
                    if (path == "value") { scalar.Value = number; return true; }
                    if (path == "min") { scalar.Min = number; return true; }
                    if (path == "max") { scalar.Max = number; return true; }
                }
                if (element.As<IUITransformCapability>() is { } transform)
                {
                    if (path == "opacity") { transform.Opacity = number; return true; }
                    if (path == "rotation") { transform.Rotation = number; return true; }
                    if (path == "position.x") { var v = transform.Position; v.x = number; transform.Position = v; return true; }
                    if (path == "position.y") { var v = transform.Position; v.y = number; transform.Position = v; return true; }
                    if (path == "scale.x") { var v = transform.Scale; v.x = number; transform.Scale = v; return true; }
                    if (path == "scale.y") { var v = transform.Scale; v.y = number; transform.Scale = v; return true; }
                }
                if (element.As<IUISizeCapability>() is { } size)
                {
                    if (path == "width") { var v = size.SizeDelta; v.x = number; size.SizeDelta = v; return true; }
                    if (path == "height") { var v = size.SizeDelta; v.y = number; size.SizeDelta = v; return true; }
                }
                if (path == "fontsize" && element.As<IUITypographyCapability>() is { } typography)
                {
                    typography.FontSize = number;
                    return true;
                }
            }
            if (element.As<IUIStyleCapability>() is { } style)
            {
                if (path.StartsWith("class.", System.StringComparison.Ordinal) &&
                    bool.TryParse(value, out var classOn))
                {
                    style.SetClass(normalizedPath.Substring("class.".Length), classOn);
                    return true;
                }
                if (path.StartsWith("token.", System.StringComparison.Ordinal))
                {
                    style.ApplyToken(normalizedPath.Substring("token.".Length), value);
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseVector2(string value, out Vector2 result)
        {
            result = Vector2.zero;
            var parts = (value ?? string.Empty).Split(',');
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return false;
            result = new Vector2(x, y);
            return true;
        }

        private async Task<TransitionHandle> BeginTransitionAsync(string screenId, UITransitionConflictPolicy policy,
            CancellationToken external = default)
        {
            while (_transitions.TryGetValue(screenId, out var current))
            {
                if (policy == UITransitionConflictPolicy.Ignore) return null;
                if (policy == UITransitionConflictPolicy.Cancel)
                    current.Cancellation.Cancel();

                if (external.CanBeCanceled)
                {
                    // Let the caller's token break out of the wait, not just the in-flight op.
                    var finished = await Task.WhenAny(
                        current.Completion.Task,
                        Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, external));
                    if (finished != current.Completion.Task)
                        return null; // caller cancelled while waiting
                }
                else
                {
                    await current.Completion.Task;
                }

                if (_lifetimeCts.IsCancellationRequested) return null;
            }

            var handle = external.CanBeCanceled
                ? new TransitionHandle(_lifetimeCts.Token, external)
                : new TransitionHandle(_lifetimeCts.Token);
            _transitions[screenId] = handle;
            return handle;
        }

        private void EndTransition(
            string screenId,
            TransitionHandle handle)
        {
            if (_transitions.TryGetValue(screenId, out var current) &&
                ReferenceEquals(current, handle))
            {
                _transitions.Remove(screenId);
            }

            handle.Completion.TrySetResult(true);
            handle.Cancellation.Dispose();
        }

        private void RollbackOpen(string screenId, UIScreenInstance instance, IUISurface surface,
            bool policyApplied, int appliedInputPolicies, bool focusTrapped, bool fromRetained)
        {
            _open.Remove(screenId);
            _modalStack.Remove(screenId);
            _backStack.Remove(screenId);
            if (focusTrapped)
                try { _focus.Release(surface, false); }
                catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }
            if (policyApplied && instance != null)
                try { _policy.Revert(instance); }
                catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }
            if (instance != null)
                for (int i = appliedInputPolicies - 1; i >= 0; i--)
                    try { _inputPolicies[i].Release(instance.Definition); }
                    catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }

            // A surface taken from the retained cache is a known-good instance: put it back so
            // its lifetime contract (KeepAlive/Pool/Preload) survives a failed open. Only
            // surfaces created during this call are destroyed.
            if (fromRetained && instance != null && ShouldRetain(instance.Definition))
            {
                try { surface.SetActive(false); }
                catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }
                _retained[screenId] = instance;
            }
            else
            {
                SafeDestroySurface(screenId, surface);
            }

            if (instance != null) instance.State = UIScreenState.Closed;
        }

        private static bool ShouldRetain(UIScreenDefinition definition)
        {
            // Dynamic overrides mutate backend capabilities. Without a backend-provided reset
            // snapshot, recreating is the only way to prevent a prior variant/breakpoint leaking
            // into the next open.
            if ((definition.variants != null && definition.variants.Length > 0) ||
                (definition.responsiveRules != null && definition.responsiveRules.Length > 0))
                return false;
            return definition.policy.lifetimePolicy == UILifetimePolicy.KeepAlive ||
                   definition.policy.lifetimePolicy == UILifetimePolicy.Pool ||
                   definition.loadStrategy == UIScreenLoadStrategy.KeepAlive ||
                   definition.loadStrategy == UIScreenLoadStrategy.Pool;
        }

        private void SafeDestroySurface(string screenId, IUISurface surface)
        {
            if (surface == null) return;
            try { surface.SetActive(false); }
            catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }
            try { surface.Destroy(); }
            catch (System.Exception ex) { RaiseScreenFaulted(screenId, ex); }
        }

        private void RaiseScreenOpened(UIScreenInstance instance)
        {
            try { ScreenOpened?.Invoke(instance); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        private void RaiseScreenClosed(UIScreenInstance instance)
        {
            try { ScreenClosed?.Invoke(instance); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        private void RaiseScreenFaulted(string screenId, System.Exception ex)
        {
            try { ScreenFaulted?.Invoke(screenId, ex); }
            catch (System.Exception subscriberException) { Debug.LogException(subscriberException); }
        }

        public void Shutdown()
        {
            _liveManagers.Remove(this);
            _lifetimeCts.Cancel();

            // Release close waiters so awaiting game code does not hang past teardown.
            foreach (var waiters in _closeWaiters.Values)
                foreach (var waiter in waiters)
                    waiter.TrySetResult(null);
            _closeWaiters.Clear();
            _closeResults.Clear();

            foreach (var transition in _transitions.Values.ToList())
            {
                transition.Cancellation.Cancel();
                transition.Completion.TrySetResult(true);
            }
            _transitions.Clear();
            foreach (var inst in _open.Values.ToList())
                inst.Surface?.Destroy();
            _open.Clear();
            foreach (var inst in _retained.Values)
                inst.Surface?.Destroy();
            _retained.Clear();
            _backStack.Clear();
            _modalStack.Clear();
            _toastQueue.Clear();
            _policy.Reset();
        }
    }
}
