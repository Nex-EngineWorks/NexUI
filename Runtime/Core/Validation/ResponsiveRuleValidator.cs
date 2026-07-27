using System.Collections.Generic;

namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Validates responsive rules: duplicate/empty rule ids, inverted resolution ranges
    /// (min &gt; max), and overlapping resolution ranges for the same input mode.
    /// </summary>
    public sealed class ResponsiveRuleValidator : IUIValidator
    {
        public string ValidatorId => "responsive-rule";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            foreach (var def in context.Definitions)
            {
                if (def == null || def.responsiveRules == null || def.responsiveRules.Length == 0)
                    continue;

                var seen = new HashSet<string>();

                for (int i = 0; i < def.responsiveRules.Length; i++)
                {
                    var r = def.responsiveRules[i];
                    if (r == null) continue;

                    if (string.IsNullOrEmpty(r.ruleId))
                        report.Add(UIValidationResult.Error(ValidatorId,
                            $"Screen '{def.ScreenId}' has a responsive rule with an empty ruleId.", def));
                    else if (!seen.Add(r.ruleId))
                        report.Add(UIValidationResult.Error(ValidatorId,
                            $"Screen '{def.ScreenId}' has duplicate responsive ruleId '{r.ruleId}'.", def));

                    if (r.minResolution.x > r.maxResolution.x || r.minResolution.y > r.maxResolution.y)
                        report.Add(UIValidationResult.Error(ValidatorId,
                            $"Responsive rule '{r.ruleId}' on '{def.ScreenId}' has min resolution greater than max.", def));

                    for (int j = i + 1; j < def.responsiveRules.Length; j++)
                    {
                        var other = def.responsiveRules[j];
                        if (other == null) continue;
                        if (other.constrainInputMode && r.constrainInputMode && other.inputMode != r.inputMode) continue;
                        if (RangesOverlap(r, other))
                            report.Add(UIValidationResult.Warning(ValidatorId,
                                $"Responsive rules '{r.ruleId}' and '{other.ruleId}' on '{def.ScreenId}' overlap for the same input mode.", def));
                    }
                }
            }
        }

        private static bool RangesOverlap(UIResponsiveRule a, UIResponsiveRule b)
        {
            bool xOverlap = a.minResolution.x <= b.maxResolution.x && b.minResolution.x <= a.maxResolution.x;
            bool yOverlap = a.minResolution.y <= b.maxResolution.y && b.minResolution.y <= a.maxResolution.y;
            return xOverlap && yOverlap;
        }
    }
}
