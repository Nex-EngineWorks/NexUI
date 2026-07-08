using System.Collections.Generic;
using emiteat.NexUI.Core.Validation;

namespace emiteat.NexUI.Editor.Validator
{
    /// <summary>Validates the declared action-key list: non-empty and unique.</summary>
    public sealed class CommandActionValidator : IProjectAssetValidator
    {
        public string Id => "command-action";

        public void Validate(NexUIValidationInput input, UIValidationReport report)
        {
            if (input.ActionKeys == null) return;

            var seen = new HashSet<string>();
            foreach (var key in input.ActionKeys)
            {
                if (string.IsNullOrEmpty(key))
                {
                    report.Add(UIValidationResult.Warning(Id, "An empty action key is declared."));
                    continue;
                }
                if (!seen.Add(key))
                    report.Add(UIValidationResult.Warning(Id, $"Duplicate action key '{key}'."));
            }
        }
    }
}
