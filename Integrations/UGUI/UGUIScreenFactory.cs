using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Instantiates a uGUI screen prefab referenced by the screen definition, parents it
    /// under the parent layer, and returns a <see cref="UGUISurface"/>.
    /// </summary>
    public sealed class UGUIScreenFactory : IUIScreenFactory
    {
        public UIRenderBackend Backend => UIRenderBackend.UGUI;

        public Task<IUISurface> CreateAsync(UIScreenDefinition definition,
            IUISurface parentLayer,
            CancellationToken ct)
        {
            if (!(definition.backendAsset.asset is GameObject prefab))
            {
                Debug.LogError(
                    $"[NexUI] UGUIScreenFactory: screen '{definition.ScreenId}' asset is not a GameObject prefab.");
                return Task.FromResult<IUISurface>(null);
            }

            Transform parent = parentLayer?.NativeRoot is GameObject parentGo ? parentGo.transform : null;

            var instance = Object.Instantiate(prefab, parent);
            instance.name = definition.ScreenId;

            // Stretch to fill the parent if it is a RectTransform.
            if (instance.transform is RectTransform rt && parent is RectTransform)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            IUISurface surface = new UGUISurface(definition.ScreenId, instance);
            return Task.FromResult(surface);
        }
    }
}
