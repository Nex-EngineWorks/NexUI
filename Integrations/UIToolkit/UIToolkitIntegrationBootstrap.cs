using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Backend-independent layer root for UI Toolkit: a full-stretch VisualElement that
    /// hosts all screens of one <see cref="UILayerType"/>.
    /// </summary>
    public sealed class UIToolkitLayerRoot : IUILayerRoot
    {
        public UILayerType LayerType { get; }
        public UIRenderBackend Backend => UIRenderBackend.UIToolkit;
        public IUISurface Surface { get; }
        public int BaseSortingOrder { get; }

        public UIToolkitLayerRoot(UILayerType layerType, VisualElement container, int baseSortingOrder)
        {
            LayerType = layerType;
            BaseSortingOrder = baseSortingOrder;
            Surface = new UIToolkitSurface($"__layer_{layerType}", container);
        }
    }

    /// <summary>
    /// Drop-in MonoBehaviour that wires the UI Toolkit backend into a UIManager: creates a
    /// layer container per layer type inside a <see cref="UIDocument"/>, and registers the
    /// screen factory, focus adapter, theme applier and the built-in motion player.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIToolkitIntegrationBootstrap : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;

        [Tooltip("Layers to create containers for, in back-to-front order.")]
        [SerializeField] private UILayerType[] _layers =
        {
            UILayerType.Background, UILayerType.HUD, UILayerType.Window,
            UILayerType.Modal, UILayerType.Toast, UILayerType.Overlay
        };

        /// <summary>The manager wired by this bootstrap; defaults to the shared NexUI.Manager.</summary>
        public UIManager Manager { get; private set; }

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            Manager = Core.NexUI.Manager;
            Register(Manager);
        }

        /// <summary>Wire the UI Toolkit backend into the given manager.</summary>
        public void Register(UIManager manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (_document == null || _document.rootVisualElement == null)
            {
                Debug.LogError("[NexUI] UIToolkitIntegrationBootstrap: no UIDocument root available.");
                return;
            }

            var root = _document.rootVisualElement;

            manager.RegisterFactory(new UIToolkitScreenFactory());
            manager.RegisterFocusAdapter(new UIToolkitFocusAdapter());

            manager.MotionPlayer ??= new BuiltInMotionPlayer();
            manager.MotionResolver ??= new MotionResolver();

            NexUIThemeAPI.RegisterApplier(new UIToolkitThemeApplier());

            int order = 0;
            foreach (var layer in _layers)
            {
                var container = new VisualElement { name = $"NexUI.Layer.{layer}" };
                container.style.position = Position.Absolute;
                container.style.left = 0; container.style.top = 0;
                container.style.right = 0; container.style.bottom = 0;
                container.pickingMode = PickingMode.Ignore;
                root.Add(container);

                manager.RegisterLayer(new UIToolkitLayerRoot(layer, container, order));
                order += 100;
            }
        }
    }
}
