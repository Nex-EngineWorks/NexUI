using System.Collections.Generic;
using emiteat.NexUI.Core.Validation;

namespace emiteat.NexUI.Editor.Validator
{
    /// <summary>Validates motion preset ids: non-empty and unique within the registry.</summary>
    public sealed class MotionPresetValidator : IProjectAssetValidator
    {
        public string Id => "motion-preset";

        public void Validate(NexUIValidationInput input, UIValidationReport report)
        {
            if (input.Motions == null || input.Motions.motions == null) return;

            var seen = new HashSet<string>();
            foreach (var preset in input.Motions.motions)
            {
                if (preset == null) continue;
                if (string.IsNullOrEmpty(preset.motionId))
                {
                    report.Add(UIValidationResult.Error(Id, $"Motion preset '{preset.name}' has an empty motionId.", preset));
                    continue;
                }
                if (!seen.Add(preset.motionId))
                    report.Add(UIValidationResult.Error(Id, $"Duplicate motionId '{preset.motionId}'.", preset));

                if ((preset.variants == null || preset.variants.Length == 0) &&
                    (preset.graph == null || !preset.graph.HasContent))
                {
                    report.Add(UIValidationResult.Warning(Id, $"Motion '{preset.motionId}' has no variants and no graph.", preset));
                }
            }
        }
    }
}
