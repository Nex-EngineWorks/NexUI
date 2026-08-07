using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using Debug = UnityEngine.Debug;

namespace emiteat.NexUI.Core
{
    /// <summary>Logs each command as it flows through the pipeline.</summary>
    public sealed class LoggingMiddleware : IUIMiddleware
    {
        public async Task InvokeAsync(IUICommand command, UICommandContext context, Func<Task> next)
        {
            Debug.Log($"[NexUI] -> command '{command.CommandId}'");
            var sw = Stopwatch.StartNew();
            try
            {
                await next();
                Debug.Log($"[NexUI] done '{command.CommandId}' ({sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NexUI] failed '{command.CommandId}': {ex}");
                throw;
            }
        }
    }

    /// <summary>Swallows and logs exceptions so a failing command cannot break the pipeline.</summary>
    public sealed class ExceptionGuardMiddleware : IUIMiddleware
    {
        public async Task InvokeAsync(IUICommand command, UICommandContext context, Func<Task> next)
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NexUI] Command '{command.CommandId}' threw and was guarded: {ex}");
            }
        }
    }
}
