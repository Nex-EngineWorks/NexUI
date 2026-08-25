#if NEXUI_HAS_ADDRESSABLES
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace emiteat.NexUI.Integrations.Addressables
{
    /// <summary>
    /// <see cref="IUIResourceProvider"/> backed by Addressables. Concurrent loads of the same
    /// key share one handle, and handles are reference-counted: every successful
    /// <see cref="LoadAssetAsync"/> hands out one reference which exactly one
    /// <see cref="Release"/> must return. Core never references Addressables.
    /// </summary>
    public sealed class AddressablesUIResourceProvider : IUIResourceProvider
    {
        private sealed class Entry
        {
            public AsyncOperationHandle Handle;
            public int RefCount;
        }

        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>();

        public async Task<T> LoadAssetAsync<T>(string key, CancellationToken ct) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (!_entries.TryGetValue(key, out var entry))
            {
                // Dedupe concurrent loads of the same key: the second caller awaits the same
                // underlying operation instead of starting a second load that would leak the
                // first handle.
                var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(key);
                entry = new Entry { Handle = handle, RefCount = 0 };
                _entries[key] = entry;
            }

            entry.RefCount++;
            try
            {
                var result = await entry.Handle.Task;

                if (ct.IsCancellationRequested)
                {
                    Release(key);
                    return null;
                }

                if (entry.Handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[NexUI] Addressables load failed for key '{key}'.");
                    Release(key);
                    return null;
                }

                return (T)(object)entry.Handle.Result;
            }
            catch
            {
                // The await itself failed (e.g. invalid key): undo our reference so the entry
                // can be cleaned up instead of leaking a permanently pinned handle.
                Release(key);
                throw;
            }
        }

        public void Release(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!_entries.TryGetValue(key, out var entry)) return;

            entry.RefCount--;
            if (entry.RefCount > 0) return;

            _entries.Remove(key);
            if (entry.Handle.IsValid())
                UnityEngine.AddressableAssets.Addressables.Release(entry.Handle);
        }
    }
}
#endif
