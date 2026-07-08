using System.Collections.Generic;

namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Validates screen variants: duplicate/empty variant ids, empty overrides, and a
    /// missing "Default" variant when variants are declared. Element-existence of
    /// override targets is checked Designer-side (element list is Designer metadata).
    /// </summary>
    public sealed class ScreenVariantValidator : IUIValidator
    {
        public string ValidatorId => "screen-variant";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            foreach (var def in context.Definitions)
            {
                if (def == null || def.variants == null || def.variants.Length == 0)
                    continue;

                var seen = new HashSet<string>();
                bool hasDefault = false;

                foreach (var v in def.variants)
                {
                    if (v == null) continue;

                    if (string.IsNullOrEmpty(v.variantId))
                    {
                        report.Add(UIValidationResult.Error(ValidatorId,
                            $"Screen '{def.ScreenId}' has a variant with an empty variantId.", def));
                        continue;
                    }

                    if (!seen.Add(v.variantId))
                        report.Add(UIValidationResult.Error(ValidatorId,
                            $"Screen '{def.ScreenId}' has duplicate variantId '{v.variantId}'.", def));

                    if (string.Equals(v.variantId, "Default", System.StringComparison.OrdinalIgnoreCase))
                        hasDefault = true;

                    if (v.overrides == null || v.overrides.Length == 0)
                        report.Add(UIValidationResult.Warning(ValidatorId,
                            $"Variant '{v.variantId}' on screen '{def.ScreenId}' has no overrides.", def));
                }

                if (!hasDefault)
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"Screen '{def.ScreenId}' declares variants but no 'Default' variant.", def));
            }
        }
    }
}
