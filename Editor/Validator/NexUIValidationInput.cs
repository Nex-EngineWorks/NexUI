using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Validation;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Editor.Validator
{
    /// <summary>Bundle of assets the editor-side validators inspect.</summary>
    public sealed class NexUIValidationInput
    {
        public UIScreenDefinition[] Screens = System.Array.Empty<UIScreenDefinition>();
        public UIMotionRegistryAsset Motions;
        public UIThemeRegistryAsset Themes;
        public string[] ActionKeys = System.Array.Empty<string>();
    }

    /// <summary>An editor-side validator that inspects a <see cref="NexUIValidationInput"/>.</summary>
    public interface IProjectAssetValidator
    {
        string Id { get; }
        void Validate(NexUIValidationInput input, UIValidationReport report);
    }
}
