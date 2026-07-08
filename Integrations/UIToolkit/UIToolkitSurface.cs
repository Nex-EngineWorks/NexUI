using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Wraps a UI Toolkit <see cref="VisualElement"/> tree as an <see cref="IUISurface"/>.
    /// Resolves child handles by element name and caches them.
    /// </summary>
    public sealed class UIToolkitSurface : IUISurface
    {
        private readonly VisualElement _root;
        private readonly Dictionary<string, IUIElementHandle> _handleCache =
            new Dictionary<string, IUIElementHandle>();

        public string ScreenId { get; }
        public UIRenderBackend Backend => UIRenderBackend.UIToolkit;
        public object NativeRoot => _root;
        public IUIElementHandle RootHandle { get; }

        public UIToolkitSurface(string screenId, VisualElement root)
        {
            ScreenId = screenId;
            _root = root;
            RootHandle = new UIToolkitElementHandle(root, screenId);
        }

        public IUIElementHandle TryFind(string elementId)
        {
            if (_handleCache.TryGetValue(elementId, out var cached))
                return cached;

            var ve = _root.Q<VisualElement>(elementId);
            if (ve == null)
                return null;

            var handle = new UIToolkitElementHandle(ve, elementId);
            _handleCache[elementId] = handle;
            return handle;
        }

        public IUIElementHandle FindRequired(string elementId)
            => TryFind(elementId) ?? throw new UIElementNotFoundException(elementId);

        public void SetActive(bool active)
            => _root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

        public void SetSortingOrder(int order)
        {
            // UI Toolkit ordering within a panel is document order; bring to front for higher orders.
            if (order >= 0) _root.BringToFront();
        }

        public void SetInputBlocking(bool blocking)
            => _root.pickingMode = blocking ? PickingMode.Position : PickingMode.Ignore;

        public void Destroy()
        {
            _handleCache.Clear();
            _root.RemoveFromHierarchy();
        }
    }
}
