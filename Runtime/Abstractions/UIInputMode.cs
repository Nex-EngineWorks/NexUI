namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// The active input paradigm the UI is being driven by. Shared across Core
    /// (responsive rules), Integrations (InputSystem) and the Designer input-mode
    /// preview. Lives in Abstractions so no spoke module has to depend on another.
    /// </summary>
    public enum UIInputMode
    {
        KeyboardMouse = 0,
        Gamepad = 1,
        Touch = 2,
        SteamDeck = 3
    }
}
