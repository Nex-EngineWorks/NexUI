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

        [Test]
        public void ValueBinder_TwoWayUpdatesStoreFromInputWithoutFeedbackLoop()
        {
            var store = new UIStateStore();
            store.Set("volume", 0.25f);
            var input = new FakeValueInput();
            var handle = new FakeElementHandle("slider")
                .With<IUIValueCapability>(input)
                .With<IUIValueInputCapability>(input);
            var binder = new UIValueBinder(UIBindingMode.TwoWay);

            binder.Bind(handle, "volume", store);
            Assert.AreEqual(0.25f, input.Value);
            input.Raise(0.8f);

            Assert.AreEqual(0.8f, store.Get<float>("volume"));
            binder.Unbind();
        }

        [Test]
        public void TextBinder_TwoWayUsesParseAndFormat()
        {
            var store = new UIStateStore();
            store.Set<object>("count", 3);
            var input = new FakeTextInput();
            var handle = new FakeElementHandle("field")
                .With<IUITextCapability>(input)
                .With<IUITextInputCapability>(input);
            var binder = new UITextBinder(UIBindingMode.TwoWay,
                value => $"#{value}", text => int.Parse(text.TrimStart('#')));

            binder.Bind(handle, "count", store);
            Assert.AreEqual("#3", input.Text);
            input.Raise("#7");

            Assert.AreEqual(7, store.Get<object>("count"));
            binder.Unbind();
        }

        [Test]
        public void PropertyValueBinder_TwoWayUsesConverterBack()
        {
            var source = new BindableProperty<int>(2);
            var input = new FakeValueInput();
            var handle = new FakeElementHandle("stepper")
                .With<IUIValueCapability>(input)
                .With<IUIValueInputCapability>(input);
            var binder = new PropertyValueBinder<int>(new IntFloatConverter(), UIBindingMode.TwoWay);

            binder.Bind(handle, source);
            Assert.AreEqual(2f, input.Value);
            input.Raise(9f);

            Assert.AreEqual(9, source.Value);
            binder.Unbind();
        }

        private sealed class IntFloatConverter : IValueConverter<int, float>
        {
            public float Convert(int source) => source;
            public int ConvertBack(float target) => UnityEngine.Mathf.RoundToInt(target);
        }
    }
}
