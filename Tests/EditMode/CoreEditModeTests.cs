using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.State;
using emiteat.NexUI.Tests.Fakes;

namespace emiteat.NexUI.Tests.EditMode
{
    public sealed class CoreEditModeTests
    {
        private static UIScreenDefinition Screen(string id)
        {
            var def = ScriptableObject.CreateInstance<UIScreenDefinition>();
            def.identity = new UIScreenIdentity { screenId = id };
            return def;
        }

        [Test]
        public void ScreenRegistry_RegisterAndGet()
        {
            var reg = new UIScreenRegistry();
            var def = Screen("HUD");
            reg.Register(def);
            Assert.IsTrue(reg.Contains("HUD"));
            Assert.AreSame(def, reg.Get("HUD"));
        }

        [Test]
        public void BackStack_PushPop_LIFO()
        {
            var stack = new UIBackStack();
            stack.Push("A");
            stack.Push("B");
            Assert.AreEqual(2, stack.Count);
            Assert.IsTrue(stack.TryPop(out var top));
            Assert.AreEqual("B", top);
        }

        [Test]
        public async Task CommandDispatcher_MiddlewareRunsInOrder()
        {
            var order = new List<string>();
            var dispatcher = new UICommandDispatcher();
            dispatcher.UseMiddleware(new RecordingMiddleware("outer", order));
            dispatcher.UseMiddleware(new RecordingMiddleware("inner", order));
            dispatcher.RegisterHandler(new TestHandler(order));

            await dispatcher.DispatchAsync(new TestCommand());

            Assert.AreEqual(new[] { "outer", "inner", "handler" }, order.ToArray());
        }

        [Test]
        public void StateStore_UsesStableSnapshot_WhenWatcherUnsubscribesDuringDispatch()
        {
            var store = new UIStateStore();
            var calls = new List<string>();
            System.IDisposable second = null;
            store.Watch<int>("value", _ =>
            {
                calls.Add("first");
                second?.Dispose();
            });
            second = store.Watch<int>("value", _ => calls.Add("second"));

            store.Set("value", 1);

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
        }

        [Test]
        public void Signal_UsesStableSnapshot_WhenListenerUnsubscribesDuringDispatch()
        {
            var signal = new UISignal<int>();
            var calls = new List<string>();
            System.IDisposable second = null;
            signal.Subscribe(_ =>
            {
                calls.Add("first");
                second?.Dispose();
            }, false);
            second = signal.Subscribe(_ => calls.Add("second"), false);

            signal.Value = 1;

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
        }

        [Test]
        public async Task UIManager_ConcurrentOpen_CreatesOnlyOneSurface()
        {
            var manager = new UIManager();
            var factory = new GatedFactory();
            var screen = Screen("HUD");
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
            screen.layer = new UIScreenLayerConfig { layerType = UILayerType.HUD, openPolicy = UIOpenPolicy.Single };
            manager.RegisterFactory(factory);
            manager.RegisterScreen(screen);

            var first = manager.OpenAsync("HUD");
            var second = manager.OpenAsync("HUD");
            Assert.AreEqual(1, factory.CreateCount);

            factory.Release();
            await UniTask.WhenAll(first, second);

            Assert.AreEqual(1, factory.CreateCount);
            Assert.IsTrue(manager.IsOpen("HUD"));
            manager.Shutdown();
        }

        [Test]
        public async Task UIManager_ToastQueue_OpensNextOnlyAfterActiveCloses()
        {
            var manager = new UIManager();
            var factory = new FakeScreenFactory();
            manager.RegisterFactory(factory);
            foreach (var id in new[] { "ToastA", "ToastB" })
            {
                var screen = Screen(id);
                screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
                screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Toast, openPolicy = UIOpenPolicy.Queue };
                manager.RegisterScreen(screen);
            }

            await manager.OpenAsync("ToastA");
            await manager.OpenAsync("ToastB");
            Assert.IsTrue(manager.IsOpen("ToastA"));
            Assert.IsFalse(manager.IsOpen("ToastB"));
            Assert.AreEqual(1, manager.ToastQueueCount);

            await manager.CloseAsync("ToastA");
            Assert.IsFalse(manager.IsOpen("ToastA"));
            Assert.IsTrue(manager.IsOpen("ToastB"));
            Assert.AreEqual(0, manager.ToastQueueCount);
            manager.Shutdown();
        }

        [Test]
        public async Task UIManager_Relations_OpenAndCloseDeclaredScreens()
        {
            var manager = new UIManager();
            manager.RegisterFactory(new FakeScreenFactory());
            foreach (var id in new[] { "A", "B", "C" })
            {
                var screen = Screen(id);
                screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
                screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Window, openPolicy = UIOpenPolicy.Single };
                if (id == "A")
                    screen.relations = new UIScreenRelationConfig { opensWith = new[] { "B" }, closes = new[] { "C" } };
                manager.RegisterScreen(screen);
            }

            await manager.OpenAsync("C");
            await manager.OpenAsync("A");

            Assert.IsTrue(manager.IsOpen("A"));
            Assert.IsTrue(manager.IsOpen("B"));
            Assert.IsFalse(manager.IsOpen("C"));
            manager.Shutdown();
        }

        [Test]
        public async Task UIManager_KeepAlive_ReusesSurfaceAfterClose()
        {
            var manager = new UIManager();
            var factory = new CountingFactory();
            var screen = Screen("Inventory");
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
            screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Window, openPolicy = UIOpenPolicy.Single };
            screen.policy = new UIScreenPolicyConfig { lifetimePolicy = UILifetimePolicy.KeepAlive };
            manager.RegisterFactory(factory);
            manager.RegisterScreen(screen);

            await manager.OpenAsync("Inventory");
            var firstSurface = manager.GetSurface("Inventory");
            await manager.CloseAsync("Inventory");
            await manager.OpenAsync("Inventory");

            Assert.AreEqual(1, factory.CreateCount);
            Assert.AreSame(firstSurface, manager.GetSurface("Inventory"));
            manager.Shutdown();
        }

        [Test]
        public async Task UIManager_Preload_ReusesInactiveSurfaceOnFirstOpen()
        {
            var manager = new UIManager();
            var factory = new CountingFactory();
            var screen = Screen("MainMenu");
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
            screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Window, openPolicy = UIOpenPolicy.Single };
            screen.loadStrategy = UIScreenLoadStrategy.Preload;
            manager.RegisterFactory(factory);
            manager.RegisterScreen(screen);

            await manager.PreloadAsync();
            Assert.AreEqual(1, factory.CreateCount);
            await manager.OpenAsync("MainMenu");

            Assert.AreEqual(1, factory.CreateCount);
            Assert.IsTrue(manager.IsOpen("MainMenu"));
            manager.Shutdown();
        }

        [Test]
        public async Task UIManager_AppliesVariantAndResponsiveOverrides()
        {
            var title = new FakeElementHandle("title").With<IUITextCapability>(new FakeText());
            var surface = new FakeSurface("Menu").AddElement("title", title);
            var manager = new UIManager
            {
                ResolutionProvider = () => new Vector2Int(800, 600),
                InputMode = UIInputMode.Gamepad
            };
            manager.RegisterFactory(new SurfaceFactory(surface));
            var screen = Screen("Menu");
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI };
            screen.layer = new UIScreenLayerConfig { layerType = UILayerType.Window, openPolicy = UIOpenPolicy.Single };
            screen.variants = new[]
            {
                new UIScreenVariant
                {
                    variantId = "compact",
                    overrides = new[]
                    {
                        new UIScreenVariantOverride { targetElementId = "title", propertyPath = "text", value = "Compact" }
                    }
                }
            };
            screen.responsiveRules = new[]
            {
                new UIResponsiveRule
                {
                    minResolution = Vector2Int.zero,
                    maxResolution = new Vector2Int(1024, 768),
                    inputMode = UIInputMode.Gamepad,
                    constrainInputMode = true,
                    overrides = new List<UIResponsiveOverride>
                    {
                        new UIResponsiveOverride { elementId = "title", propertyPath = "visible", value = "false" }
                    }
                }
            };
            manager.RegisterScreen(screen);

            await manager.OpenAsync("Menu", new UIOpenArgs { variantId = "compact" });

            Assert.AreEqual("Compact", title.As<IUITextCapability>().Text);
            Assert.IsFalse(title.As<IUIVisibilityCapability>().Visible);
            manager.Shutdown();
        }

        private sealed class TestCommand : IUICommand { public string CommandId => "test"; }

        private sealed class TestHandler : IUICommandHandler<TestCommand>
        {
            private readonly List<string> _order;
            public TestHandler(List<string> order) => _order = order;
            public UniTask HandleAsync(TestCommand command, UICommandContext context)
            {
                _order.Add("handler");
                return UniTask.CompletedTask;
            }
        }

        private sealed class RecordingMiddleware : IUIMiddleware
        {
            private readonly string _name;
            private readonly List<string> _order;
            public RecordingMiddleware(string name, List<string> order) { _name = name; _order = order; }
            public async UniTask InvokeAsync(IUICommand command, UICommandContext context, System.Func<UniTask> next)
            {
                _order.Add(_name);
                await next();
            }
        }

        private sealed class GatedFactory : IUIScreenFactory
        {
            private readonly UniTaskCompletionSource _gate = new UniTaskCompletionSource();
            public int CreateCount { get; private set; }
            public UIRenderBackend Backend => UIRenderBackend.UGUI;

            public async UniTask<IUISurface> CreateAsync(UIScreenDefinition definition, IUISurface parentLayer, CancellationToken ct)
            {
                CreateCount++;
                await _gate.Task.AttachExternalCancellation(ct);
                return new FakeSurface(definition.ScreenId);
            }

            public void Release() => _gate.TrySetResult();
        }

        private sealed class CountingFactory : IUIScreenFactory
        {
            public int CreateCount { get; private set; }
            public UIRenderBackend Backend => UIRenderBackend.UGUI;

            public UniTask<IUISurface> CreateAsync(UIScreenDefinition definition, IUISurface parentLayer, CancellationToken ct)
            {
                CreateCount++;
                return UniTask.FromResult<IUISurface>(new FakeSurface(definition.ScreenId));
            }
        }

        private sealed class SurfaceFactory : IUIScreenFactory
        {
            private readonly IUISurface _surface;
            public SurfaceFactory(IUISurface surface) => _surface = surface;
            public UIRenderBackend Backend => UIRenderBackend.UGUI;
            public UniTask<IUISurface> CreateAsync(UIScreenDefinition definition, IUISurface parentLayer, CancellationToken ct)
                => UniTask.FromResult(_surface);
        }
    }
}
