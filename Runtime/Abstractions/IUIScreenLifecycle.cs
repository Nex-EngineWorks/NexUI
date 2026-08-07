using System.Threading.Tasks;

namespace emiteat.NexUI.Abstractions
{
    /// <summary>
    /// Optional lifecycle hooks a screen controller can implement to react to
    /// open / close transitions. Implemented by user code, invoked by the UIManager.
    /// </summary>
    public interface IUIScreenLifecycle
    {
        Task OnBeforeOpenAsync(UIScreenContext context);
        Task OnAfterOpenAsync(UIScreenContext context);
        Task OnBeforeCloseAsync(UIScreenContext context);
        Task OnAfterCloseAsync(UIScreenContext context);
    }
}
