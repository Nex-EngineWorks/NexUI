using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;

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
    }
}
