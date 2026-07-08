namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Placeholder capability-binding validator. Full binding-vs-capability checks
    /// require live surfaces (Integration) or a Designer graph; this rule verifies the
    /// static preconditions that can be checked from the definition alone.
    /// </summary>
    public sealed class CapabilityBindingValidator : IUIValidator
    {
        public string ValidatorId => "capability-binding";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            foreach (var def in context.Definitions)
            {
                if (def == null) continue;

                // A focus target is declared but no asset exists to resolve it against.
                if (!string.IsNullOrEmpty(def.focus.defaultFocusElementId) &&
                    def.backendAsset.asset == null)
                {
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"'{def.identity.screenId}' declares default focus '{def.focus.defaultFocusElementId}' " +
                        "but has no backend asset to bind against.", def));
                }
            }
        }
    }
}
