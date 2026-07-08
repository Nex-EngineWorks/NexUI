namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Controls how opening a screen interacts with other screens on its layer.
    /// </summary>
    public enum UIOpenPolicy
    {
        /// <summary>Open alongside existing screens on the layer.</summary>
        Additive = 0,

        /// <summary>Close all other screens on the same layer, then open.</summary>
        ReplaceLayer = 1,

        /// <summary>Only one instance of this screen may exist; re-open is a no-op / focus.</summary>
        Single = 2,

        /// <summary>Push onto the back stack (for window / modal style navigation).</summary>
        StackPush = 3,

        /// <summary>Enqueue and present one-at-a-time (toast style).</summary>
        Queue = 4
    }
}
