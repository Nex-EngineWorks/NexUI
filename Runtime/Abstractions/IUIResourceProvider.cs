using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Abstract asset loading. A default provider can wrap Resources; an optional
    /// Addressables integration can supply an alternative implementation later.
    /// </summary>
    public interface IUIResourceProvider
    {
        Task<T> LoadAssetAsync<T>(string key, CancellationToken ct)
            where T : UnityEngine.Object;

        void Release(string key);
    }
}
