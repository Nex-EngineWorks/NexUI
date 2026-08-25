using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Tests.Fakes;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// Regression tests for UIManager edge cases: the toast-slot leak when a queued open is
    /// dropped by an Ignore conflict policy, rollback destroying retained surfaces, and uGUI
    /// input-blocking semantics.
    /// </summary>
    public sealed class UIManagerRegressionTests
    {
        private static UIScreenDefinition Screen(string id)
        {
            var def = ScriptableObject.CreateInstance<UIScreenDefinition>();
            def.identity = new UIScreenIdentity { screenId = id };
            def.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
            return def;
        }

        private static UIMotionTimeline NonEmptyTimeline()
        {
            return new UIMotionTimeline
            {
                MotionId = "regression",
                Tracks = new[]
                {
                    new UIMotionTrack
                    {
                        Property = UIMotionProperty.Opacity,
                        Easing = UIMotionEasing.Linear,
                        Duration = 0.01f,
                        Keyframes = new[] { new UIMotionKeyframe(0f, 0f), new UIMotionKeyframe(1f, 1f) }
                    }
                }
            };
        }

        [Test]
        public async Task OpenAsync_DroppedByIgnoreConflict_ReleasesToastSlot()
        {
            var manager = new UIManager();
            var factory = new GatedFactory();
            manager.RegisterFactory(factory);

            var screen = Screen("Toast");
            screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Toast, openPolicy = UIOpenPolicy.Queue };
            screen.policy = new UIScreenPolicyConfig { conflictPolicy = UITransitionConflictPolicy.Ignore };
            manager.RegisterScreen(screen);

            // A preload holds the transition so the queued opens hit the Ignore path while
            // owning the toast slot.
            var preload = manager.PreloadAsync("Toast");
            var ignored1 = manager.OpenAsync("Toast");
            var ignored2 = manager.OpenAsync("Toast");
            factory.Release();
            await Task.WhenAll(preload, ignored1, ignored2);

            Assert.IsFalse(manager.IsOpen("Toast"), "Ignore policy must drop the request.");

            // With the slot leaked, this open would be enqueued and never presented.
            await manager.OpenAsync("Toast");

            Assert.IsTrue(manager.IsOpen("Toast"), "Toast slot was not released after an Ignore-conflict drop.");
            Assert.AreEqual(0, manager.ToastQueueCount);
            manager.Shutdown();
        }

        [Test]
        public async Task OpenAsync_FailureDuringOpen_KeepsRetainedSurface()
        {
            var manager = new UIManager();
            var factory = new CountingFactory();
            manager.RegisterFactory(factory);
            manager.MotionResolver = new StubMotionResolver(NonEmptyTimeline());
            manager.MotionPlayer = new ThrowingMotionPlayer();

            var screen = Screen("Kept");
            screen.policy = new UIScreenPolicyConfig { lifetimePolicy = UILifetimePolicy.KeepAlive };
            screen.motion = new UIScreenMotionConfig
            {
                openMotion = ScriptableObject.CreateInstance<ScriptableObject>()
            };
            manager.RegisterScreen(screen);

            await manager.PreloadAsync("Kept");

            // The open fails during motion playback; the preloaded surface must survive the
            // rollback instead of being destroyed.
            LogAssert.Expect(LogType.Error, new Regex(@"\[NexUI\] OpenAsync\('Kept'\) threw during open.*simulated motion failure"));
            await manager.OpenAsync("Kept");
            Assert.IsFalse(manager.IsOpen("Kept"));

            manager.MotionPlayer = new InstantMotionPlayer();
            await manager.OpenAsync("Kept");

            Assert.IsTrue(manager.IsOpen("Kept"), "Retained surface should be reusable after a failed open.");
            Assert.AreEqual(1, factory.CreateCount,
                "Rollback destroyed the retained surface instead of restoring it.");
            manager.Shutdown();
        }

        [Test]
        public void UGUISurface_SetInputBlocking_RootStaysPermeableAndBlockerToggles()
        {
            var root = new GameObject("NexUIRegressionRoot", typeof(RectTransform));
            try
            {
                var surface = new UGUISurface("S", root);

                surface.SetInputBlocking(false);

                var group = root.GetComponent<CanvasGroup>();
                Assert.IsNotNull(group);
                Assert.IsTrue(group.blocksRaycasts,
                    "Root CanvasGroup must stay raycast-permeable or every child stops receiving clicks.");

                var blocker = root.transform.Find("NexUIInputBlocker");
                Assert.IsNotNull(blocker);
                Assert.AreEqual(0, blocker.GetSiblingIndex(), "Blocker must sit behind all content.");
                var image = blocker.GetComponent<Image>();
                Assert.IsNotNull(image);
                Assert.IsFalse(image.raycastTarget, "A non-blocking screen must let clicks pass through.");

                surface.SetInputBlocking(true);
                Assert.IsTrue(image.raycastTarget);

                surface.SetInputBlocking(false);
                Assert.IsFalse(image.raycastTarget);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task CloseLayerAsync_ClosesOnlyThatLayer()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new CountingFactory());
            foreach (var (id, layer) in new[] { ("HudA", UILayerType.HUD), ("WinA", UILayerType.Window), ("WinB", UILayerType.Window) })
            {
                var screen = Screen(id);
                screen.layer = new UIScreenLayerConfig { layerType = layer, openPolicy = UIOpenPolicy.Additive };
                manager.RegisterScreen(screen);
            }

            await manager.OpenAsync("HudA");
            await manager.OpenAsync("WinA");
            await manager.OpenAsync("WinB");

            await manager.CloseLayerAsync(UILayerType.Window);

            Assert.IsFalse(manager.IsOpen("WinA"));
            Assert.IsFalse(manager.IsOpen("WinB"));
            Assert.IsTrue(manager.IsOpen("HudA"), "Other layers must survive CloseLayerAsync.");
            manager.Shutdown();
        }

        [Test]
        public void UnregisterScreen_RemovesFutureLookupsOnly()
        {
            var manager = new UIManager();
            manager.RegisterScreen(Screen("Gone"));
            Assert.IsTrue(manager.Registry.Contains("Gone"));

            manager.UnregisterScreen("Gone");

            Assert.IsFalse(manager.Registry.Contains("Gone"));
            manager.Shutdown();
        }

        [Test]
        public async Task WaitForCloseAsync_ReceivesTheCloserResult()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new CountingFactory());
            var screen = Screen("Picker");
            screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Window, openPolicy = UIOpenPolicy.Single };
            manager.RegisterScreen(screen);

            await manager.OpenAsync("Picker");
            var wait = manager.WaitForCloseAsync("Picker");
            Assert.IsFalse(wait.IsCompleted, "An open screen must not complete the waiter yet.");

            await manager.CloseAsync("Picker", new UICloseArgs { result = "sword-042" });

            Assert.IsTrue(wait.IsCompleted);
            Assert.AreEqual("sword-042", wait.Result);
            manager.Shutdown();
        }

        [Test]
        public async Task WaitForCloseAsync_AlreadyClosed_ReturnsLastResultImmediately()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new CountingFactory());
            var screen = Screen("Dialog");
            manager.RegisterScreen(screen);

            await manager.OpenAsync("Dialog");
            await manager.CloseAsync("Dialog", new UICloseArgs { result = 42 });

            var wait = manager.WaitForCloseAsync("Dialog");
            Assert.IsTrue(wait.IsCompleted, "Awaiting a closed screen must not deadlock.");
            Assert.AreEqual(42, wait.Result);
            manager.Shutdown();
        }

        [Test]
        public async Task BackAsync_CarriesResultToWaiter()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new CountingFactory());
            var page = Screen("Page");
            page.layer = new UIScreenLayerConfig { layerType = UILayerType.Window, openPolicy = UIOpenPolicy.StackPush };
            page.policy = new UIScreenPolicyConfig { closeOnBack = true };
            manager.RegisterScreen(page);

            await manager.OpenAsync("Page");
            var wait = manager.WaitForCloseAsync("Page");

            await manager.BackAsync("cancelled-by-user");

            Assert.IsTrue(wait.IsCompleted);
            Assert.AreEqual("cancelled-by-user", wait.Result);
            Assert.IsFalse(manager.IsOpen("Page"));
            manager.Shutdown();
        }

        [Test]
        public async Task CloseOthersAsync_KeepsNamedScreen()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new CountingFactory());
            foreach (var id in new[] { "HUD", "Shop", "Settings" })
            {
                var screen = Screen(id);
                screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Window, openPolicy = UIOpenPolicy.Additive };
                manager.RegisterScreen(screen);
                await manager.OpenAsync(id);
            }

            await manager.CloseOthersAsync("HUD");

            Assert.IsTrue(manager.IsOpen("HUD"));
            Assert.IsFalse(manager.IsOpen("Shop"));
            Assert.IsFalse(manager.IsOpen("Settings"));
            manager.Shutdown();
        }

        [Test]
        public async Task CompiledProgram_OpensThroughUIManager_AndDisposesOnClose()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new emiteat.NexUI.Integrations.UGUI.NexCompiledUguiScreenFactory(
                new CountingFactory()));

            // Minimal one-panel compiled program, same shape the capability tests use.
            var node = new emiteat.NexUI.Compiled.NexNodeProgram
            {
                NodeId = "n-root", Name = "Root", ParentIndex = -1,
                Kind = emiteat.NexUI.Compiled.NexNodeKind.Panel,
                Rect = new Rect(0f, 0f, 200f, 100f), Visible = true, Text = string.Empty
            };
            var sourceMap = new emiteat.NexUI.Compiled.NexSourceMap();
            sourceMap.Add(node.NodeId, node.Name, 0, node.Name);
            var program = ScriptableObject.CreateInstance<emiteat.NexUI.Compiled.NexScreenProgram>();
            program.Initialize("CompiledScreen", new[] { node }, sourceMap,
                new emiteat.NexUI.Compiled.NexFeatureManifest(), new Vector2(1920f, 1080f), "hash");

            var screen = Screen("Compiled");
            screen.backendAsset = new UIScreenBackendAsset
            {
                backend = UIRenderBackend.UGUI,
                asset = program
            };
            manager.RegisterScreen(screen);

            await manager.OpenAsync("Compiled");
            Assert.IsTrue(manager.IsOpen("Compiled"), "compiled screen opens via lifecycle");

            var root = manager.GetSurface("Compiled")?.NativeRoot as GameObject;
            Assert.IsNotNull(root, "uGUI root exists");

            await manager.CloseAsync("Compiled");
            Assert.IsTrue(root == null, "closing destroys the compiled hierarchy (edit mode: immediate)");
            Assert.IsFalse(manager.IsOpen("Compiled"));
        }

        [Test]
        public async Task CompiledFactory_DelegatesNonProgramAssetsToFallback()
        {
            var manager = new UIManager();
            var factory = new CountingFactory();
            manager.RegisterFactory(new emiteat.NexUI.Integrations.UGUI.NexCompiledUguiScreenFactory(factory));

            var screen = Screen("Regular");
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
            manager.RegisterScreen(screen);

            await manager.OpenAsync("Regular");

            Assert.IsTrue(manager.IsOpen("Regular"));
            Assert.AreEqual(1, factory.CreateCount, "non-program assets hit the wrapped factory");
            manager.Shutdown();
        }

        [Test]
        public async Task StackSnapshot_Restore_ReopensEverything()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new CountingFactory());
            foreach (var (id, layer, push) in new[]
                     {
                         ("Hud", UILayerType.HUD, false),
                         ("Page", UILayerType.Window, true),
                         ("Modal", UILayerType.Modal, false)
                     })
            {
                var screen = Screen(id);
                screen.layer = new UIScreenLayerConfig
                {
                    layerType = layer,
                    openPolicy = push ? UIOpenPolicy.StackPush : UIOpenPolicy.Additive
                };
                screen.policy = new UIScreenPolicyConfig { closeOnBack = push };
                manager.RegisterScreen(screen);
            }

            await manager.OpenAsync("Hud");
            await manager.OpenAsync("Page");
            await manager.OpenAsync("Modal");

            var snapshot = manager.CaptureStackSnapshot();
            Assert.AreEqual(3, snapshot.Entries.Count);

            await manager.CloseAllAsync();
            Assert.AreEqual(0, manager.OpenScreens.Count);

            await manager.RestoreStackAsync(snapshot);

            Assert.IsTrue(manager.IsOpen("Hud"));
            Assert.IsTrue(manager.IsOpen("Page"));
            Assert.IsTrue(manager.IsOpen("Modal"));
            CollectionAssert.Contains(manager.BackStackSnapshot(), "Page",
                "StackPush screens must re-push onto the back stack on restore");
            manager.Shutdown();
        }

        [Test]
        public async Task ReplaceLayer_Crossfade_LeavesExactlyOneOpenPerLayer()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new CountingFactory());
            foreach (var id in new[] { "First", "Second" })
            {
                var screen = Screen(id);
                screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Window, openPolicy = UIOpenPolicy.ReplaceLayer };
                screen.policy = new UIScreenPolicyConfig { conflictPolicy = UITransitionConflictPolicy.Wait };
                manager.RegisterScreen(screen);
            }
            var closed = new List<string>();
            manager.ScreenClosed += inst => closed.Add(inst.ScreenId);

            await manager.OpenAsync("First");
            await manager.OpenAsync("Second");

            Assert.IsTrue(manager.IsOpen("Second"));
            Assert.IsFalse(manager.IsOpen("First"));
            CollectionAssert.AreEqual(new[] { "First" }, closed);
            manager.Shutdown();
        }

        private sealed class GatedFactory : IUIScreenFactory
        {
            private readonly TaskCompletionSource<bool> _gate =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int CreateCount { get; private set; }

            public UIRenderBackend Backend => UIRenderBackend.UGUI;

            public async Task<IUISurface> CreateAsync(
                UIScreenDefinition definition, IUISurface parentLayer, CancellationToken ct)
            {
                CreateCount++;
                await _gate.Task;
                return new FakeSurface(definition.ScreenId);
            }

            public void Release() => _gate.TrySetResult(true);
        }

        private sealed class CountingFactory : IUIScreenFactory
        {
            public int CreateCount { get; private set; }

            public UIRenderBackend Backend => UIRenderBackend.UGUI;

            public Task<IUISurface> CreateAsync(UIScreenDefinition definition, IUISurface parentLayer,
                CancellationToken ct)
            {
                CreateCount++;
                return Task.FromResult<IUISurface>(new FakeSurface(definition.ScreenId));
            }
        }

        private sealed class StubMotionResolver : IUIMotionResolver
        {
            private readonly UIMotionTimeline _timeline;

            public StubMotionResolver(UIMotionTimeline timeline) => _timeline = timeline;

            public UIMotionTimeline Resolve(UnityEngine.Object motionAsset)
                => motionAsset != null ? _timeline : UIMotionTimeline.Empty;
        }

        private sealed class ThrowingMotionPlayer : IUIMotionPlayer
        {
            public Task PlayAsync(IUIElementHandle target, UIMotionTimeline timeline, CancellationToken ct)
                => throw new InvalidOperationException("simulated motion failure");

            public void Stop(IUIElementHandle target) { }
        }

        private sealed class InstantMotionPlayer : IUIMotionPlayer
        {
            public Task PlayAsync(IUIElementHandle target, UIMotionTimeline timeline, CancellationToken ct)
                => Task.CompletedTask;

            public void Stop(IUIElementHandle target) { }
        }
    }
}
