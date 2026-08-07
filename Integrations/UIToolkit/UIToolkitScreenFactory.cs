using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Instantiates a UI Toolkit screen from a <see cref="VisualTreeAsset"/> referenced by
    /// the screen definition, applies any style sheets, mounts it under the parent layer,
    /// and returns a <see cref="UIToolkitSurface"/>.
    /// </summary>
    public sealed class UIToolkitScreenFactory : IUIScreenFactory
    {
        public UIRenderBackend Backend => UIRenderBackend.UIToolkit;

        public Task<IUISurface> CreateAsync(UIScreenDefinition definition,
            IUISurface parentLayer,
            CancellationToken ct)
        {
            if (!(definition.backendAsset.asset is VisualTreeAsset vta))
            {
                Debug.LogError(
                    $"[NexUI] UIToolkitScreenFactory: screen '{definition.ScreenId}' asset is not a VisualTreeAsset.");
                return Task.FromResult<IUISurface>(null);
            }

            // Clone into a fresh container so the screen has a single root element.
            var root = new VisualElement { name = definition.ScreenId };
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
            vta.CloneTree(root);

            ApplyStyleSheets(definition, root);

            if (parentLayer?.NativeRoot is VisualElement parentRoot)
                parentRoot.Add(root);
            else
                Debug.LogWarning(
                    $"[NexUI] UIToolkitScreenFactory: no parent VisualElement for '{definition.ScreenId}'. " +
                    "The screen is detached and will not render until added to a panel.");

            IUISurface surface = new UIToolkitSurface(definition.ScreenId, root);
            return Task.FromResult(surface);
        }

        private static void ApplyStyleSheets(UIScreenDefinition definition, VisualElement root)
        {
            var styles = definition.backendAsset.styleAssets;
            if (styles == null) return;
            foreach (var style in styles)
                if (style is StyleSheet sheet)
                    root.styleSheets.Add(sheet);
        }
    }
}
