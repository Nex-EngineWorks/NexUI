using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>uGUI layer root: a full-stretch child of the canvas hosting one layer's screens.</summary>
    public sealed class UGUILayerRoot : IUILayerRoot
    {
        public UILayerType LayerType { get; }
        public UIRenderBackend Backend => UIRenderBackend.UGUI;
        public IUISurface Surface { get; }
        public int BaseSortingOrder { get; }

        public UGUILayerRoot(UILayerType layerType, GameObject container, int baseSortingOrder)
        {
            LayerType = layerType;
            BaseSortingOrder = baseSortingOrder;
            Surface = new UGUISurface($"__layer_{layerType}", container);
        }
    }

    /// <summary>
    /// Drop-in MonoBehaviour that wires the uGUI backend into a UIManager: creates a
    /// full-stretch layer container per layer type under a Canvas and registers the screen
    /// factory, focus adapter, theme applier and the built-in motion player.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class UGUIIntegrationBootstrap : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;

        [Tooltip("Layers to create containers for, in back-to-front order.")]
        [SerializeField] private UILayerType[] _layers =
        {
            UILayerType.Background, UILayerType.HUD, UILayerType.Window,
            UILayerType.Modal, UILayerType.Toast, UILayerType.Overlay
        };

        public UIManager Manager { get; private set; }

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            Manager = Core.NexUI.Manager;
            Register(Manager);
        }

        /// <summary>Wire the uGUI backend into the given manager.</summary>
        public void Register(UIManager manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (_canvas == null)
            {
                Debug.LogError("[NexUI] UGUIIntegrationBootstrap: no Canvas available.");
                return;
            }

            manager.RegisterFactory(new UGUIScreenFactory());
            manager.RegisterFocusAdapter(new UGUIFocusAdapter());

            manager.MotionPlayer ??= new BuiltInMotionPlayer();
            manager.MotionResolver ??= new MotionResolver();

            NexUIThemeAPI.RegisterApplier(new UGUIThemeApplier());

            int order = 0;
            foreach (var layer in _layers)
            {
                var go = new GameObject($"NexUI.Layer.{layer}", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_canvas.transform, worldPositionStays: false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                manager.RegisterLayer(new UGUILayerRoot(layer, go, order));
                order += 100;
            }
        }
    }
}
