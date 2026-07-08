namespace emiteat.NexUI.Core.Validation
{
    /// <summary>Flags screen definitions whose backend asset is missing.</summary>
    public sealed class MissingAssetValidator : IUIValidator
    {
        public string ValidatorId => "missing-asset";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            foreach (var def in context.Definitions)
            {
                if (def == null) continue;

                if (def.backendAsset.asset == null)
                    report.Add(UIValidationResult.Error(ValidatorId,
                        $"Screen '{def.identity.screenId}' has no backend asset assigned.", def));

                if (def.validation.warnOnMissingMotion &&
                    def.motion.openMotion == null && def.motion.closeMotion == null)
                {
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Screen '{def.identity.screenId}' has no open/close motion assigned.", def));
                }
            }
        }
    }
}
