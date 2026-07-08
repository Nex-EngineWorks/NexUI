using UnityEngine;

namespace emiteat.NexUI.Core.Registry
{
    /// <summary>
    /// Central authoring list of screen definitions. Feed it to a <see cref="UIManager"/>
    /// or <see cref="UIScreenRegistry"/> in one call, and let validators / the ID generator
    /// read it as a single source of truth.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Registry/Screen Registry", fileName = "ScreenRegistry")]
    public sealed class UIScreenRegistryAsset : ScriptableObject
    {
        public UIScreenDefinition[] screens = System.Array.Empty<UIScreenDefinition>();

        public void RegisterAll(UIManager manager)
        {
            if (manager == null || screens == null) return;
            foreach (var s in screens)
                if (s != null) manager.RegisterScreen(s);
        }

        public void RegisterAll(UIScreenRegistry registry)
        {
            if (registry == null || screens == null) return;
            foreach (var s in screens)
                if (s != null) registry.Register(s);
        }
    }
}
