namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Flags screens whose backend has no registered factory in the current context.
    /// </summary>
    public sealed class ScreenFactoryAvailabilityValidator : IUIValidator
    {
        public string ValidatorId => "screen-factory-availability";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            if (context.AvailableBackends.Count == 0)
            {
                // No backend info supplied; skip rather than raise false positives.
                return;
            }

            foreach (var def in context.Definitions)
            {
                if (def == null) continue;
                if (!context.AvailableBackends.Contains(def.backendAsset.backend))
                    report.Add(UIValidationResult.Error(ValidatorId,
                        $"'{def.identity.screenId}' needs a '{def.backendAsset.backend}' factory but none is registered.", def));
            }
        }
    }
}
