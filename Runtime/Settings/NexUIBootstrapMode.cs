namespace emiteat.NexUI.Settings
{
    /// <summary>How NexUI initializes itself at runtime.</summary>
    public enum NexUIBootstrapMode
    {
        /// <summary>The game wires everything itself; NexUI does nothing automatically.</summary>
        Manual = 0,

        /// <summary>Initialize on load via [RuntimeInitializeOnLoadMethod] using the settings asset.</summary>
        RuntimeInitializeOnLoad = 1,

        /// <summary>A scene bootstrap component performs initialization.</summary>
        SceneBootstrap = 2,

        /// <summary>A DI container (e.g. VContainer) owns and initializes NexUI.</summary>
        DIContainer = 3
    }
}
