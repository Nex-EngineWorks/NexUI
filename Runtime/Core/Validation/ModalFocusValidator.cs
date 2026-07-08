namespace emiteat.NexUI.Core.Validation
{
    /// <summary>Flags modal screens that lack focus trapping / a default focus target.</summary>
    public sealed class ModalFocusValidator : IUIValidator
    {
        public string ValidatorId => "modal-focus";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            foreach (var def in context.Definitions)
            {
                if (def == null) continue;
                if (def.layer.layerType != UILayerType.Modal) continue;

                bool traps = def.focus.trapFocus || def.policy.focusPolicy == UIFocusPolicy.TrapFocus;
                if (!traps)
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Modal '{def.identity.screenId}' does not trap focus.", def));

                if (def.validation.requireDefaultFocusForModal &&
                    string.IsNullOrEmpty(def.focus.defaultFocusElementId))
                {
                    report.Add(UIValidationResult.Error(ValidatorId,
                        $"Modal '{def.identity.screenId}' requires a default focus element but none is set.", def));
                }
            }
        }
    }
}
