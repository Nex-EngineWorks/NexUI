namespace emiteat.NexUI.Core.Validation
{
    /// <summary>Flags toast screens whose policies would break one-at-a-time queueing.</summary>
    public sealed class ToastQueueValidator : IUIValidator
    {
        public string ValidatorId => "toast-queue";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            foreach (var def in context.Definitions)
            {
                if (def == null) continue;
                if (def.layer.layerType != UILayerType.Toast) continue;

                if (def.policy.pauseGameBehind)
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Toast '{def.identity.screenId}' pauses the game; toasts should be non-blocking.", def));

                if (def.policy.blockInputBehind)
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Toast '{def.identity.screenId}' blocks input behind it; toasts should not block input.", def));

                if (def.focus.trapFocus)
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Toast '{def.identity.screenId}' traps focus; toasts should not steal focus.", def));
            }
        }
    }
}
