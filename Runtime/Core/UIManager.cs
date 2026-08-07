using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
        public async Task PreloadAsync(string screenId)
        {
            if (!_registry.TryGet(screenId, out var def))
            {
                Debug.LogError($"[NexUI] PreloadAsync: unknown screenId '{screenId}'.");
                return;
            }

            var transition = await BeginTransitionAsync(screenId, def.policy.conflictPolicy);
            if (transition == null) return;
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

        public Task OpenAsync(string screenId, UIOpenArgs args = default)
            => OpenInternalAsync(screenId, args, false, new HashSet<string>());

        private async Task OpenInternalAsync(string screenId, UIOpenArgs args, bool fromToastQueue,
            HashSet<string> relationChain)
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

            var transition = await BeginTransitionAsync(screenId, def.policy.conflictPolicy);
            if (transition == null) return;

            IUISurface surface = null;
            UIScreenInstance instance = null;
            var policyApplied = false;
            var appliedInputPolicies = 0;
            var focusTrapped = false;
            var opened = false;
            var newlyOpened = false;
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
                // ReplaceLayer close transition runs.
                surface.SetActive(false);
                if (openPolicy == UIOpenPolicy.ReplaceLayer)
                    await CloseLayerExceptAsync(def.layer.layerType, screenId);
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

                instance.State = UIScreenState.Open;

                if (instance.Lifecycle != null)
                    await instance.Lifecycle.OnAfterOpenAsync(ctx);
                token.ThrowIfCancellationRequested();
                opened = true;
                newlyOpened = true;
            }
            catch (System.OperationCanceledException)
            {
                RollbackOpen(screenId, instance, surface, policyApplied, appliedInputPolicies, focusTrapped);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NexUI] OpenAsync('{screenId}') threw during open - rolling back so the rest of the UI stack keeps working: {ex}");
                RollbackOpen(screenId, instance, surface, policyApplied, appliedInputPolicies, focusTrapped);
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

        public async Task CloseAsync(string screenId, UICloseArgs args = default)
        {
            if (!_registry.TryGet(screenId, out var registeredDef) && !_open.ContainsKey(screenId))
                return;

            var conflictPolicy = _open.TryGetValue(screenId, out var current)
                ? current.Definition.policy.conflictPolicy
                : registeredDef.policy.conflictPolicy;
            var transition = await BeginTransitionAsync(screenId, conflictPolicy);
            if (transition == null) return;

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

            if (instance != null) RaiseScreenClosed(instance);
            if (wasToast)
            {
                _toastQueue.MarkFinished(screenId);
                await DrainToastQueueAsync();
            }
        }

        // ---- Toggle / Back --------------------------------------------------

        public Task ToggleAsync(string screenId)
            => IsOpen(screenId) ? CloseAsync(screenId) : OpenAsync(screenId);

        public async Task BackAsync()
        {
            while (_backStack.TryPop(out var screenId))
            {
                if (_open.TryGetValue(screenId, out var stacked) &&
                    (stacked.Definition.policy.closeOnBack || stacked.Definition.layer.openPolicy == UIOpenPolicy.StackPush))
                {
                    await CloseAsync(screenId);
                    return;
                }
            }

            // Fallback: close the top-most modal if any.
            if (_modalStack.TryGetTop(out var modalId) && _open.TryGetValue(modalId, out var modal) &&
                modal.Definition.policy.closeOnBack)
                await CloseAsync(modalId);
        }

        // ---- Internals ------------------------------------------------------

        private async Task CloseLayerExceptAsync(UILayerType layer, string keepScreenId)
        {
            var toClose = _open.Values
                .Where(i => i.Layer == layer && i.ScreenId != keepScreenId)
                .Select(i => i.ScreenId)
                .ToList();

            foreach (var id in toClose)
                await CloseAsync(id, new UICloseArgs { immediate = true });
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
            try
            {
                var asset = await ResourceProvider.LoadAssetAsync<UnityEngine.Object>(key, token);
                token.ThrowIfCancellationRequested();
                if (asset == null)
                    throw new System.InvalidOperationException(
                        $"Resource provider returned null for screen '{definition.ScreenId}' key '{key}'.");
                runtimeDefinition = UnityEngine.Object.Instantiate(definition);
                var backendAsset = runtimeDefinition.backendAsset;
                backendAsset.asset = asset;
                runtimeDefinition.backendAsset = backendAsset;
                return await factory.CreateAsync(runtimeDefinition, parent, token);
            }
            finally
            {
                ResourceProvider.Release(key);
                if (runtimeDefinition != null) UnityEngine.Object.Destroy(runtimeDefinition);
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

        private async Task<TransitionHandle> BeginTransitionAsync(string screenId, UITransitionConflictPolicy policy)
        {
            while (_transitions.TryGetValue(screenId, out var current))
            {
                if (policy == UITransitionConflictPolicy.Ignore) return null;
                if (policy == UITransitionConflictPolicy.Cancel)
                    current.Cancellation.Cancel();
                await current.Completion.Task;
                if (_lifetimeCts.IsCancellationRequested) return null;
            }

            var handle = new TransitionHandle(_lifetimeCts.Token);
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
            bool policyApplied, int appliedInputPolicies, bool focusTrapped)
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
            SafeDestroySurface(screenId, surface);
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
            _lifetimeCts.Cancel();
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
