#if NEXUI_HAS_ADDRESSABLES
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace emiteat.NexUI.Integrations.Addressables
{
    /// <summary>
    /// <see cref="IUIResourceProvider"/> backed by Addressables. Caches load handles per key
    /// and releases them on request. Core never references Addressables.
    /// </summary>
    public sealed class AddressablesUIResourceProvider : IUIResourceProvider
    {
        private readonly Dictionary<string, AsyncOperationHandle> _handles =
            new Dictionary<string, AsyncOperationHandle>();

        public async UniTask<T> LoadAssetAsync<T>(string key, CancellationToken ct) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (_handles.TryGetValue(key, out var existing) && existing.IsValid())
            {
                if (existing.Status == AsyncOperationStatus.Succeeded)
                    return existing.Result as T;
            }

            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(key);
            _handles[key] = handle;

            using (ct.Register(() => { /* let the load finish; Release() frees it */ }))
            {
                var result = await handle.Task;
                if (ct.IsCancellationRequested)
                    return null;

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[NexUI] Addressables load failed for key '{key}'.");
                    return null;
                }
                return result;
            }
        }

        public void Release(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_handles.TryGetValue(key, out var handle))
            {
                _handles.Remove(key);
                if (handle.IsValid())
                    UnityEngine.AddressableAssets.Addressables.Release(handle);
            }
        }
    }
}
#endif
