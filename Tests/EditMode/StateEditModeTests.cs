using NUnit.Framework;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.State;
using emiteat.NexUI.Tests.Fakes;

namespace emiteat.NexUI.Tests.EditMode
{
    public sealed class StateEditModeTests
    {
        [Test]
        public void StateStore_SetGet()
        {
            var store = new UIStateStore();
            store.Set("hp", 0.5f);
            Assert.AreEqual(0.5f, store.Get<float>("hp"));
            Assert.IsTrue(store.TryGet<float>("hp", out var v));
            Assert.AreEqual(0.5f, v);
        }

        [Test]
        public void StateStore_WatchFiresImmediatelyAndOnChange()
        {
            var store = new UIStateStore();
            store.Set("name", "A");
            string observed = null;
            using var sub = store.Watch<string>("name", s => observed = s);
            Assert.AreEqual("A", observed);
            store.Set("name", "B");
            Assert.AreEqual("B", observed);
        }

        [Test]
        public void DerivedState_RecomputesFromSource()
        {
            var source = new UISignal<int>(2);
            using var derived = new UIDerivedState<int, int>(source, x => x * 10);
            Assert.AreEqual(20, derived.Value);
            source.Value = 5;
            Assert.AreEqual(50, derived.Value);
        }

        [Test]
        public void TextBinder_UpdatesCapabilityFromStore()
        {
            var store = new UIStateStore();
            var text = new FakeText();
            var handle = new FakeElementHandle("label").With<IUITextCapability>(text);

            var binder = new UITextBinder();
            binder.Bind(handle, "name", store);
            store.Set("name", "Hero");

            Assert.AreEqual("Hero", text.Text);
            binder.Unbind();
        }

        [Test]
        public void ValueBinder_UpdatesCapabilityFromStore()
        {
            var store = new UIStateStore();
            var value = new FakeValue();
            var handle = new FakeElementHandle("bar").With<IUIValueCapability>(value);

            new UIValueBinder().Bind(handle, "hp", store);
            store.Set("hp", 0.75f);

            Assert.AreEqual(0.75f, value.Value);
        }
    }
}
