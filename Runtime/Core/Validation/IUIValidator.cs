namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// A single validation rule. Runtime-friendly (no Editor dependency) so the same
    /// rules can run in play mode, in tests, or later be driven by a Designer window.
    /// </summary>
    public interface IUIValidator
    {
        string ValidatorId { get; }
        void Validate(UIValidationContext context, UIValidationReport report);
    }
}
