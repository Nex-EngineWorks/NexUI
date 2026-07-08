using System.Collections.Generic;
using emiteat.NexUI.Core.Validation;

namespace emiteat.NexUI.Editor.Validator
{
    /// <summary>Validates motion variants: unique names and steps with a positive duration.</summary>
    public sealed class MotionVariantValidator : IProjectAssetValidator
    {
        public string Id => "motion-variant";

        public void Validate(NexUIValidationInput input, UIValidationReport report)
        {
            if (input.Motions == null || input.Motions.motions == null) return;

            foreach (var preset in input.Motions.motions)
            {
                if (preset == null || preset.variants == null) continue;

                var names = new HashSet<string>();
                foreach (var variant in preset.variants)
                {
                    if (variant == null) continue;
                    if (!names.Add(variant.name))
                        report.Add(UIValidationResult.Warning(Id,
                            $"Motion '{preset.motionId}' has duplicate variant name '{variant.name}'.", preset));

                    if (variant.steps == null) continue;
                    foreach (var step in variant.steps)
                    {
                        if (step.duration <= 0f)
                            report.Add(UIValidationResult.Warning(Id,
                                $"Motion '{preset.motionId}' variant '{variant.name}' has a step with non-positive duration.", preset));
                    }
                }
            }
        }
    }
}
