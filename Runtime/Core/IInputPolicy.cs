namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Applies / releases input-related side effects when a screen opens or closes
    /// (e.g. switching Input System action maps). The interface lives in Core so the
    /// UIManager can call it, while the concrete Input System reference stays in
    /// <c>Integrations.InputSystem</c>.
    /// </summary>
    public interface IInputPolicy
    {
        void Apply(UIScreenDefinition definition);
        void Release(UIScreenDefinition definition);
    }
}
