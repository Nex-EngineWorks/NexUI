using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.Overrides;
using emiteat.NexUI.Time;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// Covers the answer to "why does this say that?".
    /// </summary>
    /// <remarks>
    /// The value of this feature is entirely in the explanation being <em>specific</em>. "An
    /// interaction changed it" is true and useless on a screen with twelve rules; the tests here
    /// assert that the rule id, the binding key and the authored value all survive into the text
    /// the author reads.
    /// </remarks>
    public sealed class NexOverrideLedgerTests
    {
        private NexScreenProgram _program;
        private NexManualTime _time;
        private NexOverrideLedger _ledger;

        [SetUp]
        public void SetUp()
        {
            var nodes = new[]
            {
                new NexNodeProgram
                {
                    NodeId = "n-title", Name = "Title", ParentIndex = -1, Kind = NexNodeKind.Label,
                    Text = "Start", Visible = true
                }
            };

            var sourceMap = new NexSourceMap();
            sourceMap.Add("n-title", "Title", 0, "Root/Title");

            _program = ScriptableObject.CreateInstance<NexScreenProgram>();
            _program.Initialize("TestScreen", nodes, sourceMap, new NexFeatureManifest(),
                new Vector2(1920f, 1080f), "hash");

            _time = new NexManualTime();
            _ledger = new NexOverrideLedger(_program, _time);
        }

        [TearDown]
        public void TearDown()
        {
            if (_program != null) Object.DestroyImmediate(_program);
            _program = null;
        }

        // ---- baseline -------------------------------------------------------

        [Test]
        public void UntouchedProperty_IsReportedAsAuthored()
        {
            Assert.IsTrue(_ledger.IsAuthored(0, NexOverrideProperty.Text));
            Assert.AreEqual(0, _ledger.Count);

            var text = _ledger.Explain(0, NexOverrideProperty.Text);
            StringAssert.Contains("Root/Title", text);
            StringAssert.Contains("'Start'", text);
            StringAssert.Contains("authored", text);
        }

        // ---- the explanation ------------------------------------------------

        [Test]
        public void Explain_NamesTheSourceTheOriginAndTheAuthoredValue()
        {
            _time.Advance(12.34d);
            _ledger.Record(0, NexOverrideProperty.Text, NexOverrideSource.Interaction, "Starting", "rule-1");

            var text = _ledger.Explain(0, NexOverrideProperty.Text);

            StringAssert.Contains("'Starting'", text);
            StringAssert.Contains("Interaction", text);
            StringAssert.Contains("rule-1", text);
            StringAssert.Contains("12.34s", text);
            StringAssert.Contains("authored value was 'Start'", text,
                "The comparison with the document is the whole point.");
        }

        [Test]
        public void Explain_NamesTheBindingKeyWhenABindingWrote()
        {
            _ledger.Record(0, NexOverrideProperty.Text, NexOverrideSource.Binding, "42", "Player.Score");

            StringAssert.Contains("Player.Score", _ledger.Explain(0, NexOverrideProperty.Text));
        }

        [Test]
        public void Explain_UsesTheAuthoringPathNotTheNodeIndex()
        {
            _ledger.Record(0, NexOverrideProperty.Visible, NexOverrideSource.GameCode, "false", "tutorial");

            var text = _ledger.Explain(0, NexOverrideProperty.Visible);

            StringAssert.Contains("Root/Title", text);
            StringAssert.DoesNotContain("node 0", text);
        }

        [Test]
        public void Explain_ComparesVisibilityAgainstTheAuthoredFlag()
        {
            _ledger.Record(0, NexOverrideProperty.Visible, NexOverrideSource.GameCode, "false", null);

            StringAssert.Contains("authored value was 'true'", _ledger.Explain(0, NexOverrideProperty.Visible));
        }

        // ---- last writer wins ------------------------------------------------

        [Test]
        public void Record_KeepsOnlyTheMostRecentWriter()
        {
            _ledger.Record(0, NexOverrideProperty.Text, NexOverrideSource.Binding, "first", "Key.A");
            _time.Advance(5d);
            _ledger.Record(0, NexOverrideProperty.Text, NexOverrideSource.Interaction, "second", "rule-2");

            Assert.AreEqual(1, _ledger.Count, "A full history grows without bound and answers a question nobody asks.");

            var text = _ledger.Explain(0, NexOverrideProperty.Text);
            StringAssert.Contains("second", text);
            StringAssert.Contains("rule-2", text);
            StringAssert.DoesNotContain("Key.A", text);
        }

        [Test]
        public void TextAndVisible_AreTrackedIndependently()
        {
            _ledger.Record(0, NexOverrideProperty.Text, NexOverrideSource.Binding, "x", "k");

            Assert.IsFalse(_ledger.IsAuthored(0, NexOverrideProperty.Text));
            Assert.IsTrue(_ledger.IsAuthored(0, NexOverrideProperty.Visible));
        }

        [Test]
        public void Clear_ReturnsAPropertyToAuthored()
        {
            _ledger.Record(0, NexOverrideProperty.Text, NexOverrideSource.GameCode, "x", "k");
            _ledger.Clear(0, NexOverrideProperty.Text);

            Assert.IsTrue(_ledger.IsAuthored(0, NexOverrideProperty.Text));
        }

        [Test]
        public void ExplainAll_SaysSoWhenNothingDiffers()
        {
            StringAssert.Contains("Nothing", _ledger.ExplainAll());
        }

        [Test]
        public void ExplainAll_ListsEveryDifference()
        {
            _ledger.Record(0, NexOverrideProperty.Text, NexOverrideSource.Binding, "x", "k");
            _ledger.Record(0, NexOverrideProperty.Visible, NexOverrideSource.GameCode, "false", "c");

            var text = _ledger.ExplainAll();
            StringAssert.Contains("Text", text);
            StringAssert.Contains("Visible", text);
        }

        [Test]
        public void Record_IgnoresANegativeNodeIndex()
        {
            _ledger.Record(-1, NexOverrideProperty.Text, NexOverrideSource.GameCode, "x", "k");

            Assert.AreEqual(0, _ledger.Count);
        }

        // ---- wired to the interaction engine ---------------------------------

        [Test]
        public void InteractionSetText_IsRecordedWithTheRuleThatDidIt()
        {
            var interactions = new NexInteractionProgram();
            interactions.Actions.Add(new NexInteractionAction
            {
                Kind = NexActionKind.SetText, TargetNodeIndex = 0, StringValue = "Changed"
            });
            interactions.Rules.Add(new NexInteractionRule
            {
                RuleId = "the-rule", NodeIndex = 0, Trigger = NexTrigger.OnShow,
                Phase = NexPhase.Target, ActionStart = 0, ActionCount = 1
            });

            var program = ScriptableObject.CreateInstance<NexScreenProgram>();
            program.Initialize("TestScreen", _program.Nodes, _program.SourceMap,
                new NexFeatureManifest(), new Vector2(1920f, 1080f), "hash", interactions);

            var ledger = new NexOverrideLedger(program, _time);
            var runtime = new NexInteractionRuntime(program, new NexCommandRouter(),
                new NoState(), new NoSurface(), _time, ledger);

            runtime.Fire(0, NexTrigger.OnShow);

            var text = ledger.Explain(0, NexOverrideProperty.Text);
            StringAssert.Contains("'Changed'", text);
            StringAssert.Contains("the-rule", text);

            Object.DestroyImmediate(program);
        }

        // ---- fakes ----------------------------------------------------------

        private sealed class NoState : INexStateAccess
        {
            public bool TryGet(string key, out object value)
            {
                value = null;
                return false;
            }

            public void Set(string key, object value) { }
        }

        private sealed class NoSurface : INexScreenSurface
        {
            public void SetVisible(int nodeIndex, bool visible) { }
            public void SetText(int nodeIndex, string text) { }
        }
    }
}
