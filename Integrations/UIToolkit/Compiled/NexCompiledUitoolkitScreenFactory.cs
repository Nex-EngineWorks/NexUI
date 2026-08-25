using System;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Core;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// UIToolkit twin of <see cref="NexUI.Integrations.UGUI.NexCompiledUguiScreenFactory"/>:
    /// compiled programs open through the UIManager lifecycle; everything else delegates to the
    /// wrapped regular factory. Destroying the surface disposes the runtime.
    /// </summary>
    public sealed class NexCompiledUitoolkitScreenFactory : IUIScreenFactory
    {
        private readonly IUIScreenFactory _fallback;

        public NexCompiledUitoolkitScreenFactory(IUIScreenFactory fallback)
            => _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

        public UIRenderBackend Backend => _fallback.Backend;

        public Task<IUISurface> CreateAsync(UIScreenDefinition definition, IUISurface parentLayer,
            CancellationToken ct)
        {
            if (!(definition.backendAsset.asset is NexScreenProgram program))
                return _fallback.CreateAsync(definition, parentLayer, ct);

            var parentVe = parentLayer?.NativeRoot as VisualElement;
            var runtime = NexUIToolkitScreenBuilder.Build(program,
                new NexUIToolkitBuildOptions { Parent = parentVe });

            if (runtime == null)
                throw new InvalidOperationException(
                    $"Compiled UI Toolkit build failed for screen '{definition.ScreenId}'.");

            var surface = new UIToolkitSurface(definition.ScreenId, runtime.Root);
            return Task.FromResult<IUISurface>(new CompiledSurface(surface, runtime));
        }

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
