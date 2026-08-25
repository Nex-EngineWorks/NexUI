using System;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Core;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Opens COMPILED screens (<see cref="NexScreenProgram"/> as the screen's backend asset)
    /// through the UIManager lifecycle, delegating everything else to the wrapped regular
    /// factory. This is the bridge between the two screen systems: authored definitions keep
    /// their pipeline, compiled programs gain layers/policies/stacks/results for free.
    ///
    /// The built <see cref="NexScreenRuntime"/> is owned by the returned surface - destroying
    /// the surface disposes the runtime (subscriptions, pending interactions, hierarchy).
    /// </summary>
    public sealed class NexCompiledUguiScreenFactory : IUIScreenFactory
    {
        private readonly IUIScreenFactory _fallback;

        public NexCompiledUguiScreenFactory(IUIScreenFactory fallback)
            => _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

        public UIRenderBackend Backend => _fallback.Backend;

        public Task<IUISurface> CreateAsync(UIScreenDefinition definition, IUISurface parentLayer,
            CancellationToken ct)
        {
            if (!(definition.backendAsset.asset is NexScreenProgram program))
                return _fallback.CreateAsync(definition, parentLayer, ct);

            var parentGo = parentLayer?.NativeRoot as GameObject;
            var runtime = NexUGuiScreenBuilder.Build(program,
                new NexScreenBuildOptions { Parent = parentGo != null ? parentGo.transform : null });

            if (runtime == null)
                throw new InvalidOperationException(
                    $"Compiled uGUI build failed for screen '{definition.ScreenId}'.");

            var surface = new UGUISurface(definition.ScreenId, runtime.Root);
            return Task.FromResult<IUISurface>(new CompiledSurface(surface, runtime));
        }

        /// <summary>Surface whose Destroy also disposes the compiled runtime exactly once.</summary>
        private sealed class CompiledSurface : IUISurface
        {
            private readonly IUISurface _inner;
            private readonly IDisposable _runtime;
            private bool _destroyed;

            public CompiledSurface(IUISurface inner, IDisposable runtime)
            {
                _inner = inner;
                _runtime = runtime;
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
                if (_destroyed) return;
                _destroyed = true;
                try { _inner.Destroy(); }
                finally { _runtime.Dispose(); }
            }
        }
    }
}
