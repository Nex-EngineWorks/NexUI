using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Query;
using emiteat.NexUI.Theme;
using emiteat.NexUI.Tests.Fakes;

namespace emiteat.NexUI.Tests.PlayMode
{
    public sealed class RuntimePlayModeTests
    {
        private static IEnumerator Await(Task t)
        {
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted) throw t.Exception;
        }

        private static UIScreenDefinition Screen(string id, UILayerType layer, UIOpenPolicy policy)
        {
            var def = ScriptableObject.CreateInstance<UIScreenDefinition>();
            def.identity = new UIScreenIdentity { screenId = id };
            def.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
            def.layer = new UIScreenLayerConfig { layerType = layer, openPolicy = policy };
            return def;
        }

        [UnityTest]
        public IEnumerator UIManager_OpenAndClose()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new FakeScreenFactory());
            manager.RegisterScreen(Screen("HUD", UILayerType.HUD, UIOpenPolicy.Single));

            yield return Await(manager.OpenAsync("HUD"));
            Assert.IsTrue(manager.IsOpen("HUD"));

            yield return Await(manager.CloseAsync("HUD"));
            Assert.IsFalse(manager.IsOpen("HUD"));
        }

        [UnityTest]
        public IEnumerator UIManager_ToggleAndBack()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new FakeScreenFactory());
            manager.RegisterScreen(Screen("Pause", UILayerType.Modal, UIOpenPolicy.StackPush));

            yield return Await(manager.ToggleAsync("Pause"));
            Assert.IsTrue(manager.IsOpen("Pause"));

            yield return Await(manager.BackAsync());
            Assert.IsFalse(manager.IsOpen("Pause"));
        }

        [UnityTest]
        public IEnumerator BuiltInMotionPlayer_AnimatesOpacity()
        {
            var player = new BuiltInMotionPlayer();
            var handle = new FakeElementHandle("el");
            var transform = handle.As<IUITransformCapability>();
            transform.Opacity = 0f;

            var timeline = new UIMotionTimeline
            {
                MotionId = "fadeIn",
                Tracks = new[]
                {
                    new UIMotionTrack
                    {
                        Property = UIMotionProperty.Opacity,
                        Easing = UIMotionEasing.Linear,
                        Duration = 0.05f,
                        Keyframes = new[] { new UIMotionKeyframe(0f, 0f), new UIMotionKeyframe(1f, 1f) }
                    }
                }
            };

            yield return Await(player.PlayAsync(handle, timeline, CancellationToken.None));
            Assert.AreEqual(1f, transform.Opacity, 0.01f);
        }

        [UnityTest]
        public IEnumerator UIQuery_SuccessState()
        {
            var query = new UIQuery<int>(new QueryKey("n"), _ => Task.FromResult(7));
            yield return Await(query.RunAsync());
            Assert.IsTrue(query.State.Value.IsSuccess);
            Assert.AreEqual(7, query.State.Value.Data);
        }

        [Test]
        public void ThemeTransition_AppliesTokensThroughApplier()
        {
            var applier = new FakeThemeApplier();
            var theme = ScriptableObject.CreateInstance<UITheme>();
            theme.themeId = "t";
            theme.tokens = new[] { new ThemeToken("color.primary", "#3B82F6") };

            var handle = new FakeElementHandle("panel");
            new ThemeTransition(applier).Apply(handle, theme);

            Assert.AreEqual(1, applier.Applied.Count);
            Assert.AreEqual("color.primary", applier.Applied[0].key);
        }
    }
}
