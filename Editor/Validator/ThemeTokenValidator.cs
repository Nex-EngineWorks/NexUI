using System.Collections.Generic;
using emiteat.NexUI.Core.Validation;

namespace emiteat.NexUI.Editor.Validator
{
    /// <summary>Validates theme tokens: non-empty keys, unique per theme, non-empty values.</summary>
    public sealed class ThemeTokenValidator : IProjectAssetValidator
    {
        public string Id => "theme-token";

        public void Validate(NexUIValidationInput input, UIValidationReport report)
        {
            if (input.Themes == null || input.Themes.themes == null) return;

            foreach (var theme in input.Themes.themes)
            {
                if (theme == null || theme.tokens == null) continue;

                var keys = new HashSet<string>();
                foreach (var token in theme.tokens)
                {
                    if (token == null) continue;
                    if (string.IsNullOrEmpty(token.key))
                    {
                        report.Add(UIValidationResult.Error(Id, $"Theme '{theme.themeId}' has a token with an empty key.", theme));
                        continue;
                    }
                    if (!keys.Add(token.key))
                        report.Add(UIValidationResult.Warning(Id, $"Theme '{theme.themeId}' duplicates token '{token.key}'.", theme));
                    if (string.IsNullOrEmpty(token.value))
                        report.Add(UIValidationResult.Warning(Id, $"Theme '{theme.themeId}' token '{token.key}' has an empty value.", theme));
                }
            }
        }
    }
}
