using System.Collections;
using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using emiteat.NexUI.Flow;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.State;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// The runtime half of the vertical slice: a compiled program becomes live objects, a click
    /// reaches a handler, the handler's state change reaches the label, and the whole path is
    /// traceable back to the authoring element.
    /// </summary>
    /// <remarks>
    /// Programs are built here by hand rather than by running the compiler, so these tests fail
    /// only when the runtime is wrong. Compiler correctness is asserted separately in
    /// <c>NexScreenCompilerTests</c>; keeping the two apart means a broken lowering rule produces
    /// one red test with an obvious cause instead of a whole suite going red at once.
    /// </remarks>
    public sealed class NexCompiledScreenRuntimeTests
    {
        private NexScreenProgram _program;
        private NexScreenRuntime _runtime;
        private NexFlowMemorySink _sink;

        [SetUp]
        public void SetUp()
        {
            _sink = new NexFlowMemorySink();
            NexFlowTrace.ClearSinks();
            NexFlowTrace.AddSink(_sink);
            NexFlowTrace.Level = NexFlowLevel.Verbose;
        }

        [TearDown]
        public void TearDown()
        {
            NexFlowTrace.Level = NexFlowLevel.Off;
            NexFlowTrace.ClearSinks();

            _runtime?.Dispose();
            _runtime = null;

            if (_program != null) Object.DestroyImmediate(_program);
            _program = null;
        }

        // ---- helpers --------------------------------------------------------

        /// <summary>A panel root with a bound label and a command button underneath it.</summary>
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
                }
            };

            var sourceMap = new NexSourceMap();
            sourceMap.Add("n-root", "Root", 0, "Root");
            sourceMap.Add("n-title", "Title", 1, "Root/Title");
            sourceMap.Add("n-start", "StartButton", 2, "Root/StartButton");

            var features = new NexFeatureManifest();
            features.Require(NexFeatures.Button, "n-start", "StartButton is a button.");

            _program = ScriptableObject.CreateInstance<NexScreenProgram>();
            _program.Initialize("TestScreen", nodes, sourceMap, features, new Vector2(1920f, 1080f), "test-hash");
            return _program;
        }

        private Button StartButton() => _runtime.Find("n-start").GetComponent<Button>();

        // ---- construction ---------------------------------------------------

        [Test]
        public void Build_CreatesHierarchyMatchingTheProgram()
        {
            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(), default);

            Assert.IsNotNull(_runtime);
            Assert.AreEqual(3, _runtime.SourceMap.Program.Nodes.Length);

            var title = _runtime.Find("n-title");
            var root = _runtime.Find("n-root");

            Assert.IsNotNull(title);
            Assert.AreSame(root.transform, title.transform.parent);
            Assert.IsNotNull(title.GetComponent<TextMeshProUGUI>());
            Assert.IsNotNull(StartButton());
        }

        [Test]
        public void Build_RefusesAProgramFromADifferentCompilerVersion()
        {
            var program = BuildProgram();
            var forged = new SerializedObjectVersionOverride(program, 999);
            var diagnostics = new NexDiagnosticBag();

            _runtime = NexUGuiScreenBuilder.Build(program, default, diagnostics);
            forged.Restore();

            Assert.IsNull(_runtime, "A program the runtime cannot read must produce no screen at all.");
            Assert.IsTrue(diagnostics.Any(d => d.Code == NexDiagnosticCodes.ProgramSchemaMismatch));
        }

        [Test]
        public void SourceMap_ResolvesALiveObjectBackToItsAuthoringPath()
        {
            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(), default);

            Assert.AreEqual("Root/StartButton", _runtime.AuthoringPathOf(_runtime.Find("n-start")));
        }

        // ---- interaction ----------------------------------------------------

        [Test]
        public void Click_ReachesTheRegisteredHandler()
        {
            var router = new NexCommandRouter();
            var handled = 0;
            router.Register("Game.Start", _ => handled++);

            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(),
                new NexScreenBuildOptions { Router = router });

            StartButton().onClick.Invoke();

            Assert.AreEqual(1, handled);
        }

        [Test]
        public void Click_RecordsTheWholeChainInTheFlowTrace()
        {
            var router = new NexCommandRouter();
            router.Register("Game.Start", _ => { });

            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(),
                new NexScreenBuildOptions { Router = router });

            StartButton().onClick.Invoke();

            var record = _sink.Records.Last();
            Assert.AreEqual("TestScreen/Root/StartButton", record.Origin);
            Assert.IsTrue(record.Succeeded);

            var actions = record.Steps.Select(s => s.Action).ToArray();
            CollectionAssert.Contains(actions, "Pointer.Click");
            CollectionAssert.Contains(actions, "Trigger.OnClick");
            CollectionAssert.Contains(actions, "Dispatch");
            CollectionAssert.Contains(actions, "Invoke");
        }

        [Test]
        public void Click_WithNoHandler_FailsLoudlyInsteadOfSilently()
        {
            var router = new NexCommandRouter();
            NexDiagnostic raised = null;
            router.DiagnosticRaised += d => raised = d;

            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(),
                new NexScreenBuildOptions { Router = router });

            StartButton().onClick.Invoke();

            Assert.IsNotNull(raised, "An unhandled command must be reported, not swallowed.");
            Assert.AreEqual(NexDiagnosticCodes.NoCommandHandler, raised.Code);
            Assert.AreEqual("Root/StartButton", raised.Location.NodePath);

            var record = _sink.Records.Last();
            Assert.IsFalse(record.Succeeded);
            Assert.IsTrue(record.Steps.Any(s => s.Status == NexFlowStatus.Failed));
        }

        [Test]
        public void Click_WithThrowingHandler_ReportsAndKeepsTheScreenUsable()
        {
            var router = new NexCommandRouter();
            NexDiagnostic raised = null;
            router.DiagnosticRaised += d => raised = d;
            router.Register("Game.Start", _ => throw new System.InvalidOperationException("boom"));

            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(),
                new NexScreenBuildOptions { Router = router });

            Assert.DoesNotThrow(() => StartButton().onClick.Invoke());
            Assert.AreEqual(NexDiagnosticCodes.CommandHandlerThrew, raised?.Code);

            // The screen must still respond after a handler failed.
            Assert.DoesNotThrow(() => StartButton().onClick.Invoke());
        }

        // ---- binding --------------------------------------------------------

        [Test]
        public void TextBinding_UpdatesTheLabelWhenStateChanges()
        {
            var store = new UIStateStore();
            store.Set("Menu.Title", "Before");

            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(),
                new NexScreenBuildOptions { Store = store });

            var label = _runtime.Find("n-title").GetComponent<TextMeshProUGUI>();
            Assert.AreEqual("Before", label.text);

            store.Set("Menu.Title", "After");
            Assert.AreEqual("After", label.text);
        }

        [Test]
        public void Click_ThroughHandler_ChangesBoundText()
        {
            var store = new UIStateStore();
            store.Set("Menu.Title", "Idle");

            var router = new NexCommandRouter();
            router.Register("Game.Start", _ => store.Set("Menu.Title", "Starting"));

            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(),
                new NexScreenBuildOptions { Store = store, Router = router });

            StartButton().onClick.Invoke();

            Assert.AreEqual("Starting", _runtime.Find("n-title").GetComponent<TextMeshProUGUI>().text);
        }

        // ---- teardown -------------------------------------------------------

        [UnityTest]
        public IEnumerator Dispose_DestroysObjectsAndStopsWatchingState()
        {
            var store = new UIStateStore();
            store.Set("Menu.Title", "Before");

            _runtime = NexUGuiScreenBuilder.Build(BuildProgram(),
                new NexScreenBuildOptions { Store = store });

            var label = _runtime.Find("n-title").GetComponent<TextMeshProUGUI>();
            var root = _runtime.Root;

            _runtime.Dispose();
            yield return null; // Destroy is deferred to the end of the frame.

            Assert.IsTrue(root == null, "The screen root must be gone after Dispose.");

            // The watcher must be unsubscribed; if it is not, this write reaches a destroyed
            // component and throws - which is exactly the leak Dispose exists to prevent.
            Assert.DoesNotThrow(() => store.Set("Menu.Title", "After"));
            Assert.IsTrue(label == null);

            _runtime = null;
        }

        /// <summary>
        /// Rewrites the serialized compiler version so the version guard can be tested without a
        /// second compiler build. Restores the original value so the shared program stays valid.
        /// </summary>
        private sealed class SerializedObjectVersionOverride
        {
            private readonly NexScreenProgram _target;
            private readonly int _original;

            public SerializedObjectVersionOverride(NexScreenProgram target, int version)
            {
                _target = target;
                var field = typeof(NexScreenProgram)
                    .GetField("_compilerVersion", System.Reflection.BindingFlags.NonPublic |
                                                  System.Reflection.BindingFlags.Instance);
                _original = (int)field.GetValue(target);
                field.SetValue(target, version);
            }

            public void Restore()
            {
                var field = typeof(NexScreenProgram)
                    .GetField("_compilerVersion", System.Reflection.BindingFlags.NonPublic |
                                                  System.Reflection.BindingFlags.Instance);
                field.SetValue(_target, _original);
            }
        }
    }
}
