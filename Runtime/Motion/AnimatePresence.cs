using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Helper for enter / exit ("presence") animations around an element becoming
    /// visible or being removed. Mirrors the idea of AnimatePresence in web UI libs.
    /// </summary>
    public static class AnimatePresence
    {
        public static UniTask PlayEnterAsync(
            IUIMotionPlayer player, IUIElementHandle target,
            UIMotionPreset preset, CancellationToken ct = default)
            => Play(player, target, preset, "enter", ct);

        public static UniTask PlayExitAsync(
            IUIMotionPlayer player, IUIElementHandle target,
            UIMotionPreset preset, CancellationToken ct = default)
            => Play(player, target, preset, "exit", ct);

        private static UniTask Play(
            IUIMotionPlayer player, IUIElementHandle target,
            UIMotionPreset preset, string variant, CancellationToken ct)
        {
            if (player == null || target == null || preset == null)
                return UniTask.CompletedTask;

            var timeline = MotionCompiler.Compile(preset, variant);
            return player.PlayAsync(target, timeline, ct);
        }
    }
}
