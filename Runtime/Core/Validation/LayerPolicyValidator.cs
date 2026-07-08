namespace emiteat.NexUI.Core.Validation
{
    /// <summary>Flags inconsistent layer / open-policy combinations.</summary>
    public sealed class LayerPolicyValidator : IUIValidator
    {
        public string ValidatorId => "layer-policy";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            foreach (var def in context.Definitions)
            {
                if (def == null) continue;
                var layer = def.layer.layerType;
                var policy = def.layer.openPolicy;

                // Toasts should queue.
                if (layer == UILayerType.Toast && policy != UIOpenPolicy.Queue)
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Toast screen '{def.identity.screenId}' should use Queue open policy (found {policy}).", def));

                // Modals typically push onto the back stack or replace their layer.
                if (layer == UILayerType.Modal &&
                    policy != UIOpenPolicy.StackPush &&
                    policy != UIOpenPolicy.ReplaceLayer &&
                    policy != UIOpenPolicy.Single)
                {
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Modal screen '{def.identity.screenId}' uses '{policy}'; StackPush/ReplaceLayer/Single are recommended.", def));
                }

                // Queue policy only makes sense on the Toast layer.
                if (policy == UIOpenPolicy.Queue && layer != UILayerType.Toast)
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Screen '{def.identity.screenId}' uses Queue policy but is not on the Toast layer.", def));
            }
        }
    }
}
