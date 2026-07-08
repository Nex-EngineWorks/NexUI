using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Compiles authoring assets (variants / graph) into a runtime-playable
    /// <see cref="UIMotionTimeline"/>. This is the only place authoring data is read;
    /// the runtime players consume the compiled timeline exclusively.
    /// </summary>
    public static class MotionCompiler
    {
        public static UIMotionTimeline Compile(UIMotionPreset preset)
            => Compile(preset, preset != null ? preset.defaultVariant : null);

        public static UIMotionTimeline Compile(UIMotionPreset preset, string variantName)
        {
            if (preset == null)
                return UIMotionTimeline.Empty;

            var variant = SelectVariant(preset, variantName);
            if (variant != null && variant.steps != null && variant.steps.Length > 0)
                return CompileVariant(preset.motionId, variant);

            if (preset.graph != null && preset.graph.HasContent)
                return CompileGraph(preset.motionId, preset.graph);

            return new UIMotionTimeline { MotionId = preset.motionId, Tracks = System.Array.Empty<UIMotionTrack>() };
        }

        private static UIMotionVariant SelectVariant(UIMotionPreset preset, string variantName)
        {
            if (preset.variants == null || preset.variants.Length == 0) return null;
            if (!string.IsNullOrEmpty(variantName))
            {
                foreach (var v in preset.variants)
                    if (v != null && v.name == variantName) return v;
            }
            return preset.variants[0];
        }

        private static UIMotionTimeline CompileVariant(string motionId, UIMotionVariant variant)
        {
            var tracks = new List<UIMotionTrack>(variant.steps.Length);
            foreach (var step in variant.steps)
                tracks.Add(StepToTrack(step));

            return new UIMotionTimeline { MotionId = motionId, Tracks = tracks.ToArray() };
        }

        private static UIMotionTimeline CompileGraph(string motionId, UIMotionGraph graph)
        {
            // Flatten: a node starts after the max end-time of its dependencies.
            var endTimes = new Dictionary<string, float>();
            var tracks = new List<UIMotionTrack>();

            foreach (var node in graph.nodes)
            {
                if (node == null) continue;
                float startDelay = node.step.delay;
                if (node.dependencies != null)
                {
                    foreach (var dep in node.dependencies)
                        if (dep != null && endTimes.TryGetValue(dep, out var e) && e > startDelay)
                            startDelay = e;
                }

                var track = StepToTrack(node.step);
                track.Delay = startDelay;
                tracks.Add(track);

                if (!string.IsNullOrEmpty(node.id))
                    endTimes[node.id] = startDelay + node.step.duration;
            }

            return new UIMotionTimeline { MotionId = motionId, Tracks = tracks.ToArray() };
        }

        private static UIMotionTrack StepToTrack(UIMotionStep step)
        {
            return new UIMotionTrack
            {
                Property = step.property,
                Easing = step.easing,
                Duration = step.duration <= 0f ? 0.0001f : step.duration,
                Delay = step.delay,
                Keyframes = new[]
                {
                    new UIMotionKeyframe(0f, step.from),
                    new UIMotionKeyframe(1f, step.to)
                }
            };
        }
    }

    /// <summary>
    /// Adapter that lets Core request a compiled timeline from a motion asset without
    /// referencing the Motion assembly. Caches compiled timelines per preset.
    /// </summary>
    public sealed class MotionResolver : IUIMotionResolver
    {
        private readonly Dictionary<UIMotionPreset, UIMotionTimeline> _cache =
            new Dictionary<UIMotionPreset, UIMotionTimeline>();

        public UIMotionTimeline Resolve(UnityEngine.Object motionAsset)
        {
            if (motionAsset is UIMotionPreset preset)
            {
                if (!_cache.TryGetValue(preset, out var timeline))
                {
                    timeline = MotionCompiler.Compile(preset);
                    _cache[preset] = timeline;
                }
                return timeline;
            }
            return UIMotionTimeline.Empty;
        }

        public void Invalidate(UIMotionPreset preset)
        {
            if (preset != null) _cache.Remove(preset);
        }
    }
}
