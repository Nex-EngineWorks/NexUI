using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Integrations.UIToolkit;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.Motion;
using emiteat.NexUI.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// That one compiled screen builds and behaves the same on both backends.
    /// </summary>
    /// <remarks>
    /// This is the test the product's central claim rests on: the author never picks a backend, so
    /// a screen has to come out the same on either. Until the UI Toolkit compiled builder existed
    /// that claim had one implementation behind it and nothing checking it.
    ///
    /// The assertions compare what the <em>author</em> can observe - structure, text, visibility,
    /// bound values, command dispatch - and deliberately not pixels. The two engines will never
    /// rasterise identically, and a test that demanded they did would fail on every Unity update
    /// while telling nobody anything about whether the screen works.
    /// </remarks>
    public sealed class NexBackendParityTests
    {
        private NexScreenProgram _program;
        private NexScreenRuntime _ugui;
        private NexUIToolkitScreenRuntime _uitk;
        private GameObject _canvas;
        private VisualElement _panel;
        private UIMotionPreset _motionPreset;
        private UIMotionRegistryAsset _motionRegistry;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _panel = new VisualElement();
        }

        [TearDown]
        public void TearDown()
        {
            _ugui?.Dispose();
            _uitk?.Dispose();
            if (_program != null) Object.DestroyImmediate(_program);
            if (_motionPreset != null) Object.DestroyImmediate(_motionPreset);
            if (_motionRegistry != null) Object.DestroyImmediate(_motionRegistry);
            if (_canvas != null) Object.DestroyImmediate(_canvas);

            _ugui = null;
            _uitk = null;
            _program = null;
            _canvas = null;
            _panel = null;
            _motionPreset = null;
            _motionRegistry = null;
        }

        /// <summary>A panel root with a bound label, a command button and a hidden node.</summary>
        private NexScreenProgram BuildProgram(string commandId = "Game.Start", string textKey = "Menu.Title")
        {
            var nodes = new[]
            {
                new NexNodeProgram
                {
                    NodeId = "n-root", Name = "Root", ParentIndex = -1, Kind = NexNodeKind.Panel,
                    Rect = new Rect(0f, 0f, 400f, 300f), Anchor = NexAnchor.TopLeft,
                    Tint = Color.gray, TextColor = Color.white, FontSize = 14, Visible = true,
                    Text = string.Empty, TextBindingKey = string.Empty, CommandId = string.Empty
                },
                new NexNodeProgram
                {
                    NodeId = "n-title", Name = "Title", ParentIndex = 0, Kind = NexNodeKind.Label,
                    Rect = new Rect(20f, 20f, 200f, 40f), Anchor = NexAnchor.TopLeft,
                    Tint = Color.clear, TextColor = Color.white, FontSize = 24, Visible = true,
                    Text = "Authored", TextBindingKey = textKey, CommandId = string.Empty
                },
                new NexNodeProgram
                {
                    NodeId = "n-start", Name = "StartButton", ParentIndex = 0, Kind = NexNodeKind.Button,
                    Rect = new Rect(20f, 100f, 160f, 48f), Anchor = NexAnchor.TopLeft,
                    Tint = Color.blue, TextColor = Color.white, FontSize = 18, Visible = true,
                    Text = "Start", TextBindingKey = string.Empty, CommandId = commandId
                },
                new NexNodeProgram
                {
                    NodeId = "n-secret", Name = "Secret", ParentIndex = 0, Kind = NexNodeKind.Panel,
                    Rect = new Rect(0f, 200f, 80f, 40f), Anchor = NexAnchor.TopLeft,
                    Tint = Color.red, TextColor = Color.white, FontSize = 14, Visible = false,
                    Text = string.Empty, TextBindingKey = string.Empty, CommandId = string.Empty
                }
            };

            var sourceMap = new NexSourceMap();
            sourceMap.Add("n-root", "Root", 0, "Root");
            sourceMap.Add("n-title", "Title", 1, "Root/Title");
            sourceMap.Add("n-start", "StartButton", 2, "Root/StartButton");
            sourceMap.Add("n-secret", "Secret", 3, "Root/Secret");

            _program = ScriptableObject.CreateInstance<NexScreenProgram>();
            _program.Initialize("ParityScreen", nodes, sourceMap, new NexFeatureManifest(),
                new Vector2(1920f, 1080f), "parity-hash");
            return _program;
        }

        private void BuildBoth(UIStateStore store = null, NexCommandRouter router = null)
        {
            var program = BuildProgram();

            _ugui = NexUGuiScreenBuilder.Build(program, new NexScreenBuildOptions
            {
                Parent = _canvas.transform, Store = store, Router = router
            });

            _uitk = NexUIToolkitScreenBuilder.Build(program, new NexUIToolkitBuildOptions
            {
                Parent = _panel, Store = store, Router = router
            });
        }

        [Test]
        public void BothBackendsBuildTheSameProgram()
        {
            BuildBoth();

            Assert.IsNotNull(_ugui, "the uGUI backend produced no screen");
            Assert.IsNotNull(_uitk, "the UI Toolkit backend produced no screen");
            Assert.AreEqual(_ugui.ScreenId, _uitk.ScreenId);
        }

        [Test]
        public async Task BothBackendsConsumeCompiledMotionFromTheRuntimeRegistry()
        {
            var program = BuildProgram();
            var button = program.Nodes[2];
            button.Motion = new NexMotionProgram
            {
                MotionId = "parity-motion",
                InitialVariant = "initial",
                AnimateVariant = "animate",
                ExitVariant = "exit"
            };
            program.Nodes[2] = button;

            _motionPreset = ScriptableObject.CreateInstance<UIMotionPreset>();
            _motionPreset.motionId = "parity-motion";
            _motionPreset.variants = new[]
            {
                MotionVariant("initial", UIMotionProperty.Opacity),
                MotionVariant("animate", UIMotionProperty.ScaleX),
                MotionVariant("exit", UIMotionProperty.ScaleY)
            };
            _motionRegistry = ScriptableObject.CreateInstance<UIMotionRegistryAsset>();
            _motionRegistry.motions = new[] { _motionPreset };
            var player = new RecordingMotionPlayer();

            _ugui = NexUGuiScreenBuilder.Build(program, new NexScreenBuildOptions
            {
                Parent = _canvas.transform,
                MotionRegistry = _motionRegistry,
                MotionPlayer = player
            });
            _uitk = NexUIToolkitScreenBuilder.Build(program, new NexUIToolkitBuildOptions
            {
                Parent = _panel,
                MotionRegistry = _motionRegistry,
                MotionPlayer = player
            });

            CollectionAssert.AreEqual(new[]
            {
                UIMotionProperty.Opacity, UIMotionProperty.ScaleX,
                UIMotionProperty.Opacity, UIMotionProperty.ScaleX
            }, player.Played);

            await _ugui.PlayExitMotionsAsync();
            await _uitk.PlayExitMotionsAsync();
            Assert.AreEqual(UIMotionProperty.ScaleY, player.Played[player.Played.Count - 1]);
            Assert.AreEqual(2, player.Played.FindAll(p => p == UIMotionProperty.ScaleY).Count);
        }

        /// <summary>
        /// The source map is what the debugger, the flow trace and every test read, so both
        /// backends have to populate it for the same nodes.
        /// </summary>
        [Test]
        public void BothBackendsRegisterEveryNodeInTheSourceMap()
        {
            BuildBoth();

            for (int i = 0; i < _program.Nodes.Length; i++)
            {
                Assert.IsNotNull(_ugui.SourceMap.InstanceAt(i),
                    "uGUI registered nothing for node " + i);
                Assert.IsNotNull(_uitk.SourceMap.InstanceAt(i),
                    "UI Toolkit registered nothing for node " + i);
            }
        }

        [Test]
        public void BothBackendsFindANodeByItsAuthoringId()
        {
            BuildBoth();

            Assert.IsNotNull(_ugui.Find("n-start"));
            Assert.IsNotNull(_uitk.Find("n-start"));
        }

        /// <summary>
        /// Parenting is the structure the author drew, so a mismatch here is a wrong screen.
        /// </summary>
        [Test]
        public void BothBackendsParentChildrenToTheSameNode()
        {
            BuildBoth();

            var uguiTitle = _ugui.Find("n-title");
            var uguiRoot = _ugui.Find("n-root");
            Assert.AreSame(uguiRoot.transform, uguiTitle.transform.parent);

            var uitkTitle = _uitk.Find("n-title");
            var uitkRoot = _uitk.Find("n-root");
            Assert.AreSame(uitkRoot, uitkTitle.parent);
        }

        [Test]
        public void BothBackendsHideANodeTheProgramMarksInvisible()
        {
            BuildBoth();

            Assert.IsFalse(_ugui.Find("n-secret").activeSelf);
            Assert.AreEqual(DisplayStyle.None, _uitk.Find("n-secret").resolvedStyle.display);
        }

        /// <summary>
        /// A bound label must show the store's value on both backends, not the authored literal.
        /// </summary>
        [Test]
        public void ABoundLabelShowsTheSameTextOnBothBackends()
        {
            var store = new UIStateStore();
            store.Set("Menu.Title", "Bound Title");

            BuildBoth(store);

            Assert.AreEqual("Bound Title", TextOfUgui("n-title"));
            Assert.AreEqual("Bound Title", TextOfUiToolkit("n-title"));
        }

        [Test]
        public void SettingTextFromGameCodeReachesBothBackends()
        {
            BuildBoth();

            _ugui.SetText("n-title", "From Code");
            _uitk.SetText("n-title", "From Code");

            Assert.AreEqual("From Code", TextOfUgui("n-title"));
            Assert.AreEqual("From Code", TextOfUiToolkit("n-title"));
        }

        [Test]
        public void HidingFromGameCodeReachesBothBackends()
        {
            BuildBoth();

            _ugui.SetVisible("n-title", false);
            _uitk.SetVisible("n-title", false);

            Assert.IsFalse(_ugui.Find("n-title").activeSelf);
            Assert.AreEqual(DisplayStyle.None, _uitk.Find("n-title").resolvedStyle.display);
        }

        /// <summary>
        /// Both backends must refuse a program from another compiler version rather than guessing.
        /// </summary>
        [Test]
        public void BothBackendsRefuseAProgramFromADifferentCompilerVersion()
        {
            var program = BuildProgram();
            var field = typeof(NexScreenProgram).GetField("_compilerVersion",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(program, NexScreenProgram.CurrentCompilerVersion - 1);

            var uguiDiagnostics = new NexDiagnosticBag();
            var uitkDiagnostics = new NexDiagnosticBag();

            var ugui = NexUGuiScreenBuilder.Build(program,
                new NexScreenBuildOptions { Parent = _canvas.transform }, uguiDiagnostics);
            var uitk = NexUIToolkitScreenBuilder.Build(program,
                new NexUIToolkitBuildOptions { Parent = _panel }, uitkDiagnostics);

            Assert.IsNull(ugui);
            Assert.IsNull(uitk);
            Assert.IsTrue(uguiDiagnostics.HasErrors);
            Assert.IsTrue(uitkDiagnostics.HasErrors);
        }

        /// <summary>
        /// Disposal must leave nothing behind on either backend.
        /// </summary>
        /// <remarks>
        /// The mechanism differs - uGUI destroys objects, UI Toolkit detaches a managed tree - so
        /// this checks the observable outcome rather than the mechanism. uGUI destruction is
        /// deferred to the end of the frame, so the assertion yields one frame first.
        /// </remarks>
        [UnityTest]
        public System.Collections.IEnumerator DisposalDetachesTheScreenOnBothBackends()
        {
            BuildBoth();

            var uguiRoot = _ugui.Root;
            var uitkRoot = _uitk.Root;

            _ugui.Dispose();
            _uitk.Dispose();
            yield return null;

            Assert.IsTrue(uguiRoot == null, "the uGUI root should have been destroyed");
            Assert.IsNull(uitkRoot.parent, "the UI Toolkit root should have been detached");

            _ugui = null;
            _uitk = null;
        }

        private string TextOfUgui(string nodeId)
        {
            var go = _ugui.Find(nodeId);
            var text = go.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            return text != null ? text.text : null;
        }

        private string TextOfUiToolkit(string nodeId)
        {
            var element = _uitk.Find(nodeId);
            if (element is TextElement own) return own.text;

            var child = element.Q<TextElement>();
            return child != null ? child.text : null;
        }

        private static UIMotionVariant MotionVariant(string name, UIMotionProperty property)
            => new UIMotionVariant
            {
                name = name,
                steps = new[]
                {
                    new UIMotionStep { property = property, from = 0f, to = 1f, duration = 0.1f }
                }
            };

        private sealed class RecordingMotionPlayer : IUIMotionPlayer
        {
            public readonly List<UIMotionProperty> Played = new List<UIMotionProperty>();

            public Task PlayAsync(IUIElementHandle target, UIMotionTimeline timeline,
                CancellationToken ct)
            {
                Played.Add(timeline.Tracks[0].Property);
                return Task.CompletedTask;
            }

            public void Stop(IUIElementHandle target) { }
        }
    }
}
