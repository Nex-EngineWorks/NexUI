using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Creates a backend-specific <see cref="IUISurface"/> from a screen definition.
    /// Lives in Core (not Abstractions) because it references <see cref="UIScreenDefinition"/>.
    /// One factory is registered per backend.
    /// </summary>
    public interface IUIScreenFactory
    {
        UIRenderBackend Backend { get; }

        UniTask<IUISurface> CreateAsync(
            UIScreenDefinition definition,
            IUISurface parentLayer,
            CancellationToken ct
        );
    }
}
