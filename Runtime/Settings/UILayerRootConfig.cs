using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Settings
{
    /// <summary>
    /// Declarative description of a layer root for a backend bootstrap to materialize.
    /// Data only ??the actual container (VisualElement / GameObject) is created by the
    /// Integration bootstrap that reads these configs.
    /// </summary>
    [Serializable]
    public struct UILayerRootConfig
    {
        public UILayerType layerType;
        public UIRenderBackend backend;
        public int baseSortingOrder;
    }
}
