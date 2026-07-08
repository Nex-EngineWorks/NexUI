using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;
using emiteat.NexUI.Query;

namespace emiteat.NexUI.Tests.EditMode
{
    public sealed class MotionThemeQueryEditModeTests
    {
        [Test]
        public void MotionCompiler_CompilesVariantIntoTracks()
        {
            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            preset.motionId = "PopupIn";
            preset.variants = new[]
            {
                new UIMotionVariant { name = "default", steps = new[] { UIMotionStep.Fade(0f, 1f, 0.2f) } }
            };

            var timeline = MotionCompiler.Compile(preset);
            Assert.AreEqual(1, timeline.Tracks.Length);
            Assert.AreEqual(UIMotionProperty.Opacity, timeline.Tracks[0].Property);
            Assert.Greater(timeline.TotalDuration, 0f);
        }

        [Test]
        public void MotionCompiler_CompilesGraphWithDependencies()
        {
            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            preset.motionId = "Seq";
            preset.variants = System.Array.Empty<UIMotionVariant>();
            preset.graph = new UIMotionGraph
            {
                nodes = new[]
                {
                    new UIMotionGraph.Node { id = "a", step = UIMotionStep.Fade(0f, 1f, 0.2f) },
                    new UIMotionGraph.Node
                    {
                        id = "b",
                        step = new UIMotionStep { property = UIMotionProperty.ScaleX, from = 0f, to = 1f, duration = 0.2f },
                        dependencies = new[] { "a" }
                    }
                }
            };

            var timeline = MotionCompiler.Compile(preset);
            Assert.AreEqual(2, timeline.Tracks.Length);
            // Second track should start after the first (delay >= first duration).
            Assert.GreaterOrEqual(timeline.Tracks[1].Delay, 0.2f);
        }

        [Test]
        public void ThemeRegistry_RegisterAndResolveToken()
        {
            var theme = ScriptableObject.CreateInstance<UITheme>();
            theme.themeId = "dark";
            theme.tokens = new[] { new ThemeToken("color.primary", "#3B82F6") };

            var registry = new ThemeRegistry();
            registry.Register(theme);

            Assert.IsTrue(registry.TryGet("dark", out var got));
            Assert.IsTrue(got.TryGet("color.primary", out var value));
            Assert.AreEqual("#3B82F6", value);
        }

        [Test]
        public void RuntimeTokenOverride_OverridesBaseTheme()
        {
            var theme = ScriptableObject.CreateInstance<UITheme>();
            theme.themeId = "base";
            theme.tokens = new[] { new ThemeToken("color.text", "#FFFFFF") };

            var overrides = new RuntimeTokenOverride(theme);
            Assert.AreEqual("#FFFFFF", overrides.Resolve("color.text"));
            overrides.Set("color.text", "#000000");
            Assert.AreEqual("#000000", overrides.Resolve("color.text"));
        }

        [Test]
        public void QueryCache_SetGetAndInvalidate()
        {
            var cache = new QueryCache();
            var key = new QueryKey("users");
            cache.Set(key, 42);
            Assert.IsTrue(cache.TryGet<int>(key, out var v, out _));
            Assert.AreEqual(42, v);
            cache.Invalidate(key);
            Assert.IsFalse(cache.TryGet<int>(key, out _, out _));
        }
    }
}
