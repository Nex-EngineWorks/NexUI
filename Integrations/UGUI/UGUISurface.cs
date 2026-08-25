using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Wraps an instantiated uGUI hierarchy as an <see cref="IUISurface"/>. Elements are
    /// resolved by <see cref="NxUGuiBindingTag"/> id (falling back to child name search).
    /// </summary>
    public sealed class UGUISurface : IUISurface
    {
        private const string InputBlockerName = "NexUIInputBlocker";

        private readonly GameObject _root;
        private readonly Dictionary<string, IUIElementHandle> _handleCache =
            new Dictionary<string, IUIElementHandle>();
        private readonly Dictionary<string, GameObject> _tagged =
            new Dictionary<string, GameObject>();
        private Image _inputBlocker;

        public string ScreenId { get; }
        public UIRenderBackend Backend => UIRenderBackend.UGUI;
        public object NativeRoot => _root;
        public IUIElementHandle RootHandle { get; }

        public UGUISurface(string screenId, GameObject root)
        {
            ScreenId = screenId;
            _root = root;
            EnsureOwnCanvas(root);
            RootHandle = new UGUIElementHandle(root, screenId);
            IndexTags();
        }

        /// <summary>
        /// B4 (performance): gives the screen its own nested <see cref="Canvas"/> by default so
        /// this screen's rebuilds/animations don't force a full-hierarchy rebatch of sibling
        /// screens sharing a parent overlay Canvas. A nested Canvas inherits render mode,
        /// EventSystem and GraphicRaycaster from its parent - it does not need its own, and
        /// adding one here would double-raycast input. Only adds one when the prefab/screen
        /// asset doesn't already define its own (screen authors who need custom sort order /
        /// render-mode control keep full control by adding a Canvas themselves).
        /// </summary>
        private static void EnsureOwnCanvas(GameObject root)
        {
            if (root == null || root.GetComponent<Canvas>() != null) return;
            root.AddComponent<Canvas>();
        }

        private void IndexTags()
        {
            var tags = _root.GetComponentsInChildren<NxUGuiBindingTag>(includeInactive: true);
            foreach (var tag in tags)
            {
                var id = tag.ResolveId;
                if (string.IsNullOrEmpty(id)) continue;
                if (_tagged.TryGetValue(id, out var duplicate))
                {
                    Debug.LogError($"[NexUI] Duplicate uGUI binding id '{id}' on '{duplicate.name}' and '{tag.name}'. The first object remains indexed.", _root);
                    continue;
                }
                _tagged[id] = tag.gameObject;
            }
        }

        public IUIElementHandle TryFind(string elementId)
        {
            if (_handleCache.TryGetValue(elementId, out var cached))
                return cached;

            GameObject go = null;
            if (_tagged.TryGetValue(elementId, out var tagged))
                go = tagged;
            else
            {
                var t = FindChildByName(_root.transform, elementId);
                if (t != null) go = t.gameObject;
            }

            if (go == null)
                return null;

            var handle = new UGUIElementHandle(go, elementId);
            _handleCache[elementId] = handle;
            return handle;
        }

        public IUIElementHandle FindRequired(string elementId)
            => TryFind(elementId) ?? throw new UIElementNotFoundException(elementId);

        public void SetActive(bool active) => _root.SetActive(active);

        public void SetSortingOrder(int order)
        {
            var canvas = _root.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = order;
            }
            else
            {
                _root.transform.SetSiblingIndex(Mathf.Max(0, order));
            }
        }

        public void SetInputBlocking(bool blocking)
        {
            var group = _root.GetComponent<CanvasGroup>();
            if (group == null) group = _root.AddComponent<CanvasGroup>();
            // CanvasGroup.blocksRaycasts applies to every descendant, so setting it false here
            // made non-blocking screens fully click-through. The root stays raycast-permeable;
            // a dedicated transparent blocker image behind the content swallows rays instead.
            group.blocksRaycasts = true;
            group.interactable = true;
            EnsureInputBlocker().raycastTarget = blocking;
        }

        private Image EnsureInputBlocker()
        {
            if (_inputBlocker != null) return _inputBlocker;

            var blockerTransform = _root.transform.Find(InputBlockerName);
            GameObject blockerGo = blockerTransform != null ? blockerTransform.gameObject : null;
            if (blockerGo == null)
            {
                blockerGo = new GameObject(InputBlockerName, typeof(RectTransform));
                var rect = (RectTransform)blockerGo.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.transform.SetParent(_root.transform, false);
                rect.transform.SetAsFirstSibling();
            }

            var image = blockerGo.GetComponent<Image>();
            if (image == null) image = blockerGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            _inputBlocker = image;
            return image;
        }

        public void Destroy()
        {
            _handleCache.Clear();
            _tagged.Clear();
            if (_root == null) return;
            // Edit mode requires DestroyImmediate; deferred Destroy is play-mode only. Matches
            // NexScreenRuntime so compiled screens behave identically through both paths.
            if (Application.isPlaying) Object.Destroy(_root);
            else Object.DestroyImmediate(_root);
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildByName(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
