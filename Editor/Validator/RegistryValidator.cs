using emiteat.NexUI.Core.Validation;

namespace emiteat.NexUI.Editor.Validator
{
    /// <summary>Validates registries for null entries and empty content.</summary>
    public sealed class RegistryValidator : IProjectAssetValidator
    {
        public string Id => "registry";

        public void Validate(NexUIValidationInput input, UIValidationReport report)
        {
            if (input.Screens == null || input.Screens.Length == 0)
                report.Add(UIValidationResult.Info(Id, "No screen definitions supplied to validation."));

            CheckNulls(input.Screens, "screen definition", report);

            if (input.Motions != null)
                CheckNulls(input.Motions.motions, "motion preset", report, input.Motions);

            if (input.Themes != null)
                CheckNulls(input.Themes.themes, "theme", report, input.Themes);
        }

        private void CheckNulls(System.Array array, string label, UIValidationReport report, UnityEngine.Object owner = null)
        {
            if (array == null) return;
            for (int i = 0; i < array.Length; i++)
            {
                if (array.GetValue(i) == null)
                    report.Add(UIValidationResult.Warning(Id, $"Registry has a null {label} entry at index {i}.", owner));
            }
        }
    }
}
