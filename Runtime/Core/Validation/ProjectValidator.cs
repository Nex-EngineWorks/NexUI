using System.Collections.Generic;

namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Runs a collection of validators over a context and aggregates a report.
    /// Defaults to the full built-in rule set; callers may supply their own list.
    /// </summary>
    public sealed class ProjectValidator
    {
        private readonly List<IUIValidator> _validators;

        public ProjectValidator(IEnumerable<IUIValidator> validators = null)
        {
            _validators = validators != null
                ? new List<IUIValidator>(validators)
                : CreateDefault();
        }

        public static List<IUIValidator> CreateDefault() => new List<IUIValidator>
        {
            new DuplicateScreenIdValidator(),
            new MissingAssetValidator(),
            new LayerPolicyValidator(),
            new ModalFocusValidator(),
            new ToastQueueValidator(),
            new BackendMismatchValidator(),
            new CapabilityBindingValidator(),
            new ScreenFactoryAvailabilityValidator(),
            new ScreenVariantValidator(),
            new ResponsiveRuleValidator(),
            new ScreenLoadingStrategyValidator(),
        };

        public void Add(IUIValidator validator) => _validators.Add(validator);

        public UIValidationReport Validate(UIValidationContext context)
        {
            var report = new UIValidationReport();
            foreach (var v in _validators)
                v.Validate(context, report);
            return report;
        }
    }
}
