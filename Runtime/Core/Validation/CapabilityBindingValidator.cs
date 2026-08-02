namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Validates static focus preconditions and, when a live surface is supplied, every
    /// element/capability declared by the screen contract.
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

                ValidateContract(def, context, report);
            }
        }

        private void ValidateContract(Core.UIScreenDefinition definition, UIValidationContext context,
            UIValidationReport report)
        {
            var contract = definition.contract;
            if (contract == null) return;

            var screenId = definition.identity.screenId;
            if (!string.IsNullOrEmpty(contract.screenId) && contract.screenId != screenId)
                report.Add(UIValidationResult.Warning(ValidatorId,
                    $"Contract '{contract.name}' targets '{contract.screenId}' but is assigned to '{screenId}'.", contract));

            if (!context.LiveSurfaces.TryGetValue(screenId, out var surface) || surface == null)
                return;

            foreach (var requirement in contract.requiredElements)
            {
                if (requirement == null || string.IsNullOrWhiteSpace(requirement.elementId)) continue;
                var element = surface.TryFind(requirement.elementId);
                if (element == null)
                {
                    var message = $"Screen '{screenId}' does not provide contract element '{requirement.elementId}'.";
                    report.Add(requirement.required
                        ? UIValidationResult.Error(ValidatorId, message, contract)
                        : UIValidationResult.Warning(ValidatorId, message, contract));
                    continue;
                }

                foreach (var capability in requirement.requiredCapabilities)
                    if (!HasCapability(element, capability))
                        report.Add(UIValidationResult.Error(ValidatorId,
                            $"Element '{screenId}/{requirement.elementId}' does not provide '{capability}'.", contract));
            }
        }

        private static bool HasCapability(Abstractions.IUIElementHandle element, string capability)
        {
            switch ((capability ?? string.Empty).Trim())
            {
                case nameof(Abstractions.IUITextCapability): return element.Has<Abstractions.IUITextCapability>();
                case nameof(Abstractions.IUITextInputCapability): return element.Has<Abstractions.IUITextInputCapability>();
                case nameof(Abstractions.IUIValueCapability): return element.Has<Abstractions.IUIValueCapability>();
                case nameof(Abstractions.IUIValueInputCapability): return element.Has<Abstractions.IUIValueInputCapability>();
                case nameof(Abstractions.IUIVisibilityCapability): return element.Has<Abstractions.IUIVisibilityCapability>();
                case nameof(Abstractions.IUIInteractableCapability): return element.Has<Abstractions.IUIInteractableCapability>();
                case nameof(Abstractions.IUIClickCapability): return element.Has<Abstractions.IUIClickCapability>();
                case nameof(Abstractions.IUIStyleCapability): return element.Has<Abstractions.IUIStyleCapability>();
                case nameof(Abstractions.IUITransformCapability): return element.Has<Abstractions.IUITransformCapability>();
                case nameof(Abstractions.IUISizeCapability): return element.Has<Abstractions.IUISizeCapability>();
                case nameof(Abstractions.IUIColorCapability): return element.Has<Abstractions.IUIColorCapability>();
                case nameof(Abstractions.IUITypographyCapability): return element.Has<Abstractions.IUITypographyCapability>();
                default: return false;
            }
        }
    }
}
