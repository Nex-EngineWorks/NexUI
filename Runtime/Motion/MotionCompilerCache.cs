using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Caches compiled timelines per preset so repeated plays avoid recompilation.
    /// Shared cache behind <see cref="MotionResolver"/> and available to advanced callers.
    /// </summary>
    public sealed class MotionCompilerCache
    {
        private readonly Dictionary<UIMotionPreset, UIMotionTimeline> _cache =
            new Dictionary<UIMotionPreset, UIMotionTimeline>();

        public UIMotionTimeline GetOrCompile(UIMotionPreset preset)
        {
            if (preset == null) return UIMotionTimeline.Empty;
            if (!_cache.TryGetValue(preset, out var timeline))
            {
                timeline = MotionCompiler.Compile(preset);
                _cache[preset] = timeline;
            }
            return timeline;
        }

        public UIMotionTimeline GetOrCompile(UIMotionPreset preset, string variant)
        {
            // Variant-specific results are not cached by key here; compile on demand.
            if (preset == null) return UIMotionTimeline.Empty;
            return MotionCompiler.Compile(preset, variant);
        }

        public void Invalidate(UIMotionPreset preset)
        {
            if (preset != null) _cache.Remove(preset);
        }

        public void Clear() => _cache.Clear();
    }
}
