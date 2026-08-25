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
        /// <summary>
        /// Recorded sort orders per mounted screen root, so surfaces in the SAME layer container can
        /// be interleaved by priority. UI Toolkit has no z-index - ordering is document order - so
        /// this reorders sibling roots to approximate one, without touching unrelated elements.
        /// </summary>
        private static readonly Dictionary<VisualElement, int> Orders =
            new Dictionary<VisualElement, int>();

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
            var parent = _root.parent;
            if (parent == null)
            {
                // Not mounted yet: remember the intent; ApplyRecordedOrder runs on mount via the
                // next open's SetSortingOrder call once the parent exists.
                Orders[_root] = order;
                return;
            }

            Orders[_root] = order;
            ReorderSiblings(parent);
        }

        /// <summary>
        /// Rearranges only the tracked sibling roots (screen surfaces) inside
        /// <paramref name="parent"/> by recorded order; unmanaged elements keep their positions.
        /// </summary>
        private static void ReorderSiblings(VisualElement parent)
        {
            // Prune entries whose elements left this hierarchy (destroyed screens).
            foreach (var key in new List<VisualElement>(Orders.Keys))
                if (key.panel == null)
                    Orders.Remove(key);

            var trackedSlots = new List<int>();
            var tracked = new List<(VisualElement Element, int Order)>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent[i];
                if (!Orders.TryGetValue(child, out var order)) continue;
                trackedSlots.Add(i);
                tracked.Add((child, order));
            }
            if (tracked.Count < 2) return;

            // Stable: ties keep their previous relative stacking.
            for (var i = 1; i < tracked.Count; i++)
                for (var j = i; j > 0 && tracked[j - 1].Order > tracked[j].Order; j--)
                    (tracked[j - 1], tracked[j]) = (tracked[j], tracked[j - 1]);

            for (var i = 0; i < tracked.Count; i++)
                tracked[i].Element.RemoveFromHierarchy();
            for (var i = 0; i < tracked.Count; i++)
                parent.Insert(trackedSlots[i], tracked[i].Element);
        }

        public void SetInputBlocking(bool blocking)
            => _root.pickingMode = blocking ? PickingMode.Position : PickingMode.Ignore;

        public void Destroy()
        {
            _handleCache.Clear();
            Orders.Remove(_root);
            _root.RemoveFromHierarchy();
        }
    }
}
