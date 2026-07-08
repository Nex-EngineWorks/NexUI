namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Validates each screen's load strategy against its backend asset and lifetime
    /// policy: Addressable needs a referenced asset/key, KeepAlive warns about memory,
    /// and Pool should pair with a Pool lifetime policy.
    /// </summary>
    public sealed class ScreenLoadingStrategyValidator : IUIValidator
    {
        public string ValidatorId => "screen-loading-strategy";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            foreach (var def in context.Definitions)
            {
                if (def == null) continue;

                switch (def.loadStrategy)
                {
                    case UIScreenLoadStrategy.Addressable:
                        if (def.backendAsset.asset == null)
                            report.Add(UIValidationResult.Warning(ValidatorId,
                                $"Screen '{def.ScreenId}' uses Addressable loading but has no referenced asset/key.", def));
                        break;

                    case UIScreenLoadStrategy.KeepAlive:
                        report.Add(UIValidationResult.Info(ValidatorId,
                            $"Screen '{def.ScreenId}' is KeepAlive; it stays resident for the whole session.", def));
                        break;

                    case UIScreenLoadStrategy.Pool:
                        if (def.policy.lifetimePolicy != UILifetimePolicy.Pool)
                            report.Add(UIValidationResult.Info(ValidatorId,
                                $"Screen '{def.ScreenId}' uses Pool loading but its lifetime policy is not Pool.", def));
                        break;
                }
            }
        }
    }
}
