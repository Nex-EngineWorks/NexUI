using emiteat.NexUI.Compiled;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// Value bindings on controls the compiled program describes by capability rather than by kind.
    /// </summary>
    /// <remarks>
    /// The compiled node kinds are panel, image, label and button; a slider is a panel that
    /// declares <see cref="NexNodeCapabilities.Value"/> and names a control. These tests are what
    /// keep that indirection honest - if the capability stops producing a real control, the value
    /// binding silently does nothing, which is the exact failure the design was chosen to avoid.
    /// </remarks>
    public sealed class NexValueBindingTests
    {
        private NexScreenProgram _program;
        private NexScreenRuntime _runtime;
        private UIStateStore _store;

        [SetUp]
        public void SetUp() => _store = new UIStateStore();

        [TearDown]
        public void TearDown()
        {
            _runtime?.Dispose();
            _runtime = null;
            if (_program != null) Object.DestroyImmediate(_program);
            _program = null;
        }

        private NexScreenProgram BuildProgram(string controlId, NexNodeCapabilities capabilities,
            string valueKey, UIBindingMode mode = UIBindingMode.OneWay, float min = 0f, float max = 1f)
        {
            var nodes = new[]
            {
                new NexNodeProgram
                {
                    NodeId = "n-root", Name = "Root", ParentIndex = -1, Kind = NexNodeKind.Panel,
                    Rect = new Rect(0f, 0f, 400f, 300f), Anchor = NexAnchor.TopLeft,
                    Visible = true, Text = string.Empty
                },
                new NexNodeProgram
                {
                    NodeId = "n-control", Name = "Control", ParentIndex = 0, Kind = NexNodeKind.Panel,
                    Rect = new Rect(20f, 20f, 200f, 30f), Anchor = NexAnchor.TopLeft,
                    Visible = true, Text = string.Empty,
                    ControlId = controlId, Capabilities = capabilities,
                    ValueBindingKey = valueKey, ValueBindingMode = mode,
                    ValueMin = min, ValueMax = max
                }
            };

            var sourceMap = new NexSourceMap();
            sourceMap.Add("n-root", "Root", 0, "Root");
            sourceMap.Add("n-control", "Control", 1, "Root/Control");

            _program = ScriptableObject.CreateInstance<NexScreenProgram>();
            _program.Initialize("ValueScreen", nodes, sourceMap, new NexFeatureManifest(),
                new Vector2(1920f, 1080f), "value-hash");
            return _program;
        }

        private const NexNodeCapabilities SliderCaps =
            NexNodeCapabilities.Value | NexNodeCapabilities.UserEditable;

        private const NexNodeCapabilities ToggleCaps =
            NexNodeCapabilities.Value | NexNodeCapabilities.BooleanValue |
            NexNodeCapabilities.UserEditable | NexNodeCapabilities.Click;

        private GameObject Control() => _runtime.Find("n-control");

        [Test]
        public void ACapabilityDeclaringNodeGetsARealControl()
        {
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Slider", SliderCaps, "audio.volume"),
                new NexScreenBuildOptions { Store = _store });

            var slider = Control().GetComponent<Slider>();
            Assert.IsNotNull(slider, "A node declaring Value with ControlId 'Slider' must build one.");
            Assert.IsNotNull(slider.fillRect, "A slider with no fill accepts drags and shows nothing.");
            Assert.IsNotNull(slider.handleRect);
        }

        [Test]
        public void StateReachesTheControl()
        {
            _store.Set("audio.volume", 0.75f);

            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Slider", SliderCaps, "audio.volume"),
                new NexScreenBuildOptions { Store = _store });

            Assert.AreEqual(0.75f, Control().GetComponent<Slider>().value, 0.001f);
        }

        [Test]
        public void LaterStateChangesReachTheControl()
        {
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Slider", SliderCaps, "audio.volume"),
                new NexScreenBuildOptions { Store = _store });

            _store.Set("audio.volume", 0.25f);

            Assert.AreEqual(0.25f, Control().GetComponent<Slider>().value, 0.001f);
        }

        [Test]
        public void OneWayDoesNotWriteUserEditsBack()
        {
            _store.Set("audio.volume", 0.5f);
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Slider", SliderCaps, "audio.volume"),
                new NexScreenBuildOptions { Store = _store });

            Control().GetComponent<Slider>().value = 0.9f;

            Assert.AreEqual(0.5f, _store.Get<float>("audio.volume"), 0.001f,
                "A one-way binding must leave the source alone.");
        }

        [Test]
        public void TwoWayWritesUserEditsBack()
        {
            _store.Set("audio.volume", 0.5f);
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Slider", SliderCaps, "audio.volume", UIBindingMode.TwoWay),
                new NexScreenBuildOptions { Store = _store });

            Control().GetComponent<Slider>().value = 0.9f;

            Assert.AreEqual(0.9f, _store.Get<float>("audio.volume"), 0.001f);
        }

        [Test]
        public void ABindingWriteDoesNotEchoBackAsAUserEdit()
        {
            // The loop this guards against: the store writes the slider, the slider reports a
            // change, the change writes the store. It does not crash - it just never settles,
            // and it only shows up as a screen that fights the game logic.
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Slider", SliderCaps, "audio.volume", UIBindingMode.TwoWay),
                new NexScreenBuildOptions { Store = _store });

            var writes = 0;
            using (_store.Watch<object>("audio.volume", _ => writes++))
            {
                writes = 0;
                _store.Set("audio.volume", 0.3f);

                Assert.AreEqual(1, writes,
                    "One store write must produce one notification, not a ping-pong with the control.");
            }
        }

        [Test]
        public void AToggleReportsItsStateAsANumber()
        {
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Toggle", ToggleCaps, "options.fullscreen", UIBindingMode.TwoWay),
                new NexScreenBuildOptions { Store = _store });

            var toggle = Control().GetComponent<Toggle>();
            Assert.IsNotNull(toggle);

            toggle.isOn = true;
            Assert.AreEqual(1f, _store.Get<float>("options.fullscreen"), 0.001f,
                "One value path covers every control, so a toggle reports 0 or 1.");
        }

        [Test]
        public void ANodeWithoutTheCapabilityGetsNoControl()
        {
            // A plain panel that happens to name a control must not grow one: capabilities are
            // what the compiler validated against, so they are what the builder honours.
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Slider", NexNodeCapabilities.None, "audio.volume"),
                new NexScreenBuildOptions { Store = _store });

            Assert.IsNull(Control().GetComponent<Slider>());
        }

        [Test]
        public void AnUnknownControlIdIsIgnoredRatherThanThrowing()
        {
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Holographic", SliderCaps, "audio.volume"),
                new NexScreenBuildOptions { Store = _store });

            Assert.IsNotNull(Control(), "The node is still built and laid out.");
            Assert.IsNull(Control().GetComponent<Slider>());
        }

        [Test]
        public void TheSliderRangeComesFromTheProgram()
        {
            _runtime = NexUGuiScreenBuilder.Build(
                BuildProgram("Slider", SliderCaps, "hp.current", min: 0f, max: 100f),
                new NexScreenBuildOptions { Store = _store });

            var slider = Control().GetComponent<Slider>();
            Assert.AreEqual(0f, slider.minValue, 0.001f);
            Assert.AreEqual(100f, slider.maxValue, 0.001f);
        }
    }
}
