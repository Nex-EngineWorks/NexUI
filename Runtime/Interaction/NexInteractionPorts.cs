namespace emiteat.NexUI.Interaction
{
    /// <summary>
    /// The interaction engine's window onto game state.
    /// </summary>
    /// <remarks>
    /// A port, not a reference to the real store. The engine needs exactly two operations, and
    /// depending on <c>emiteat.NexUI.State</c> for them would tie interaction execution to one
    /// state implementation - which is precisely what stops a project from bringing its own.
    /// The adapter that wraps <c>UIStateStore</c> lives with the backend that has both.
    /// </remarks>
    public interface INexStateAccess
    {
        bool TryGet(string key, out object value);

        void Set(string key, object value);
    }

    /// <summary>
    /// The interaction engine's window onto the built screen.
    /// </summary>
    /// <remarks>
    /// Addressed by compiled node index rather than by GameObject or VisualElement, so the engine
    /// is identical on every backend and can run headless in a test with a fake surface. The
    /// compiler already resolved authored element ids into these indices, so nothing here has to
    /// search the hierarchy.
    /// </remarks>
    public interface INexScreenSurface
    {
        void SetVisible(int nodeIndex, bool visible);

        void SetText(int nodeIndex, string text);
    }
}
