using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Input for validators: the definitions under test plus which backends have a
    /// registered factory (so availability / mismatch checks can run without Unity Editor).
    /// </summary>
    public sealed class UIValidationContext
    {
        public IReadOnlyList<UIScreenDefinition> Definitions { get; }
        public ISet<UIRenderBackend> AvailableBackends { get; }
        public IReadOnlyDictionary<string, IUISurface> LiveSurfaces { get; }

        public UIValidationContext(
            IReadOnlyList<UIScreenDefinition> definitions,
            ISet<UIRenderBackend> availableBackends = null,
            IReadOnlyDictionary<string, IUISurface> liveSurfaces = null)
        {
            Definitions = definitions ?? new List<UIScreenDefinition>();
            AvailableBackends = availableBackends ?? new HashSet<UIRenderBackend>();
            LiveSurfaces = liveSurfaces ?? new Dictionary<string, IUISurface>();
        }
    }
}
