using Cysharp.Threading.Tasks;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Optional lifecycle hooks a screen controller can implement to react to
    /// open / close transitions. Implemented by user code, invoked by the UIManager.
    /// </summary>
    public interface IUIScreenLifecycle
    {
        UniTask OnBeforeOpenAsync(UIScreenContext context);
        UniTask OnAfterOpenAsync(UIScreenContext context);
        UniTask OnBeforeCloseAsync(UIScreenContext context);
        UniTask OnAfterCloseAsync(UIScreenContext context);
    }
}
