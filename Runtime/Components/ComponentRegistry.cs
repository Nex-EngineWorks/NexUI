using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Components
{
    /// <summary>
    /// Maps a component contract type to a backend factory that wraps an existing
    /// element handle as that component. Integrations register factories; user code
    /// resolves a typed component from a handle without knowing the backend.
    /// </summary>
    public sealed class ComponentRegistry
    {
        // (contractType, backend) -> factory(handle) -> component
        private readonly Dictionary<(Type, UIRenderBackend), Func<IUIElementHandle, object>> _factories =
            new Dictionary<(Type, UIRenderBackend), Func<IUIElementHandle, object>>();

        public void Register<TContract>(UIRenderBackend backend, Func<IUIElementHandle, TContract> factory)
            where TContract : class
        {
            if (factory == null) return;
            _factories[(typeof(TContract), backend)] = h => factory(h);
        }

        public TContract Wrap<TContract>(IUIElementHandle handle) where TContract : class
        {
            if (handle == null) return null;
            if (_factories.TryGetValue((typeof(TContract), handle.Backend), out var factory))
                return factory(handle) as TContract;

            Debug.LogWarning(
                $"[NexUI] No component factory for '{typeof(TContract).Name}' on backend '{handle.Backend}'.");
            return null;
        }

        public bool CanWrap<TContract>(UIRenderBackend backend) where TContract : class
            => _factories.ContainsKey((typeof(TContract), backend));
    }
}
