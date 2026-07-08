using System.Collections.Generic;

namespace emiteat.NexUI.Core.Validation
{
    /// <summary>Flags duplicate or empty screen ids.</summary>
    public sealed class DuplicateScreenIdValidator : IUIValidator
    {
        public string ValidatorId => "duplicate-screen-id";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            var seen = new HashSet<string>();
            foreach (var def in context.Definitions)
            {
                if (def == null) continue;
                var id = def.identity.screenId;

                if (string.IsNullOrEmpty(id))
                {
                    report.Add(UIValidationResult.Error(ValidatorId, $"Screen '{def.name}' has an empty screenId.", def));
                    continue;
                }

                if (!seen.Add(id))
                    report.Add(UIValidationResult.Error(ValidatorId, $"Duplicate screenId '{id}'.", def));
            }
        }
    }
}
