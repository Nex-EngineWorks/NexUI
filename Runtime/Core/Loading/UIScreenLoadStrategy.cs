namespace emiteat.NexUI.Core
{
    /// <summary>
    /// How the UIManager should provision the backend instance for a screen.
    /// </summary>
    public enum UIScreenLoadStrategy
    {
        /// <summary>Instantiate up-front during startup/preload.</summary>
        Preload = 0,

        /// <summary>Instantiate the first time the screen is opened.</summary>
        LazyLoad = 1,

        /// <summary>Load the backend asset through an Addressables provider.</summary>
        Addressable = 2,

        /// <summary>Return the instance to a pool on close instead of destroying it.</summary>
        Pool = 3,

        /// <summary>Keep the instance alive for the lifetime of the session.</summary>
        KeepAlive = 4
    }
}
