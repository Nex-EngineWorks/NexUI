using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        public async Task CompiledMotionBinding_PlaysEntryAndStateVariants()
        {
            var preset = CreateStatePreset();
            var pointer = new FakePointer();
            var focus = new FakeFocus();
            var target = new FakeHandle(pointer, focus);
            var player = new RecordingPlayer();
            var binding = new CompiledMotionBinding(target, preset, player,
                "initial", "animate", "exit", "hover", "pressed", "focus").Attach();

            await binding.PlayEntryAsync();
            CollectionAssert.AreEqual(new[]
            {
                UIMotionProperty.Opacity,
                UIMotionProperty.PositionY
            }, player.Played);

            pointer.Enter();
            pointer.Down();
            pointer.Up();
            focus.Focus();
            focus.Blur();

            CollectionAssert.AreEqual(new[]
            {
                UIMotionProperty.Opacity,
                UIMotionProperty.PositionY,
                UIMotionProperty.ScaleX,
                UIMotionProperty.Rotation,
                UIMotionProperty.ScaleX,
                UIMotionProperty.PositionX,
                UIMotionProperty.PositionY
            }, player.Played);

            await binding.PlayExitAsync();
            Assert.AreEqual(UIMotionProperty.ScaleY, player.Played[player.Played.Count - 1]);
            binding.Dispose();
        }

        [Test]
        public void CompiledMotionBinding_DisposeUnsubscribesGestureEvents()
        {
            var pointer = new FakePointer();
            var focus = new FakeFocus();
            var player = new RecordingPlayer();
            var binding = new CompiledMotionBinding(new FakeHandle(pointer, focus),
                CreateStatePreset(), player, null, "animate", null, "hover", null, null).Attach();

            binding.Dispose();
            pointer.Enter();
            focus.Focus();

            Assert.IsEmpty(player.Played);
            Assert.AreEqual(1, player.StopCount);
        }

        [Test]
        public void UGUIElementHandle_ProvidesPointerAndFocusCapabilities()
        {
            var go = new GameObject("gesture-handle", typeof(RectTransform));
            try
            {
                go.AddComponent<Integrations.UGUI.UGUIGestureRelay>();
                var handle = new Integrations.UGUI.UGUIElementHandle(go);
                Assert.IsTrue(handle.Has<IUIPointerCapability>());
                Assert.IsTrue(handle.Has<IUIFocusCapability>());
                Assert.IsNotNull(go.GetComponent<Integrations.UGUI.UGUIGestureRelay>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
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

        private static UIMotionPreset CreateStatePreset()
        {
            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            preset.motionId = "compiled-state";
            preset.variants = new[]
            {
                Variant("initial", UIMotionProperty.Opacity),
                Variant("animate", UIMotionProperty.PositionY),
                Variant("exit", UIMotionProperty.ScaleY),
                Variant("hover", UIMotionProperty.ScaleX),
                Variant("pressed", UIMotionProperty.Rotation),
                Variant("focus", UIMotionProperty.PositionX)
            };
            return preset;
        }

        private static UIMotionVariant Variant(string name, UIMotionProperty property)
            => new UIMotionVariant
            {
                name = name,
                steps = new[]
                {
                    new UIMotionStep { property = property, from = 0f, to = 1f, duration = 0.1f }
                }
            };

        private sealed class RecordingPlayer : IUIMotionPlayer
        {
            public readonly List<UIMotionProperty> Played = new List<UIMotionProperty>();
            public int StopCount;

            public Task PlayAsync(IUIElementHandle target, UIMotionTimeline timeline,
                CancellationToken ct)
            {
                Played.Add(timeline.Tracks[0].Property);
                return Task.CompletedTask;
            }

            public void Stop(IUIElementHandle target) => StopCount++;
        }

        private sealed class FakeHandle : IUIElementHandle
        {
            private readonly FakePointer _pointer;
            private readonly FakeFocus _focus;

            public FakeHandle(FakePointer pointer, FakeFocus focus)
            {
                _pointer = pointer;
                _focus = focus;
            }

            public string Id => "fake";
            public UIRenderBackend Backend => UIRenderBackend.UGUI;
            public object Native => null;

            public bool Has<TCapability>() where TCapability : class => As<TCapability>() != null;

            public TCapability As<TCapability>() where TCapability : class
            {
                if (typeof(TCapability) == typeof(IUIPointerCapability)) return _pointer as TCapability;
                if (typeof(TCapability) == typeof(IUIFocusCapability)) return _focus as TCapability;
                return null;
            }
        }

        private sealed class FakePointer : IUIPointerCapability
        {
            public event Action PointerEntered;
            public event Action PointerExited;
            public event Action PointerDown;
            public event Action PointerUp;

            public void Enter() => PointerEntered?.Invoke();
            public void Exit() => PointerExited?.Invoke();
            public void Down() => PointerDown?.Invoke();
            public void Up() => PointerUp?.Invoke();
        }

        private sealed class FakeFocus : IUIFocusCapability
        {
            public event Action Focused;
            public event Action Blurred;
            public bool HasFocus { get; private set; }

            public void Focus() { HasFocus = true; Focused?.Invoke(); }
            public void Blur() { HasFocus = false; Blurred?.Invoke(); }
        }
    }
}
