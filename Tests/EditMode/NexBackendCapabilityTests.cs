using System;
using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Compiled;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// That the capability table and the "what does this screen use" reader agree, and that the
    /// compatibility report is built from both rather than from a third opinion.
    /// </summary>
    /// <remarks>
    /// The failure this guards is specific: a report that says a screen is fine on a backend that
    /// then does not draw it, or the reverse. That was possible while each applier decided both
    /// halves inline and the report decided them again somewhere else.
    /// </remarks>
    public sealed class NexBackendCapabilityTests
    {
        private NexScreenProgram _program;

        [TearDown]
        public void TearDown()
        {
            if (_program != null) UnityEngine.Object.DestroyImmediate(_program);
            _program = null;
        }

        private NexScreenProgram Screen(params NexNodeProgram[] nodes)
        {
            var sourceMap = new NexSourceMap();
            for (int i = 0; i < nodes.Length; i++)
                sourceMap.Add(nodes[i].NodeId, nodes[i].Name, i, nodes[i].Name);

            _program = ScriptableObject.CreateInstance<NexScreenProgram>();
            _program.Initialize("CapabilityScreen", nodes, sourceMap, new NexFeatureManifest(),
                new Vector2(1920f, 1080f), "hash");
            return _program;
        }

        private static NexNodeProgram Node(string id) => new NexNodeProgram
        {
            NodeId = "n-" + id, Name = id, ParentIndex = -1, Kind = NexNodeKind.Panel,
            Rect = new Rect(0f, 0f, 100f, 40f), Visible = true, Text = string.Empty
        };

        // ---- the two halves agree ------------------------------------------

        /// <summary>
        /// Every capability has a display name, or a report row prints an enum member at the author.
        /// </summary>
        [Test]
        public void EveryCapabilityHasAnAuthorFacingName()
        {
            foreach (NexCapability capability in Enum.GetValues(typeof(NexCapability)))
            {
                var name = NexBackendCapabilities.DisplayName(capability);
                Assert.That(name, Is.Not.Null.And.Not.Empty);
                Assert.That(name, Is.Not.EqualTo(capability.ToString()),
                    capability + " falls through to its enum name; give it a name an author reads.");
            }
        }

        /// <summary>
        /// A capability nothing can detect is a row that can never fire.
        /// </summary>
        /// <remarks>
        /// Every member of the table has to be reachable from a compiled program, or the report is
        /// promising to check something it never looks at.
        /// </remarks>
        [Test]
        public void EveryCapabilityCanBeDetected()
        {
            var undetectable = new List<NexCapability>();

            foreach (NexCapability capability in Enum.GetValues(typeof(NexCapability)))
                if (!TryBuildNodeUsing(capability, out _)) undetectable.Add(capability);

            Assert.That(undetectable, Is.Empty,
                "These capabilities have no node shape that reports using them, so no report will " +
                "ever mention them: " + string.Join(", ", undetectable));
        }

        /// <summary>
        /// <c>Uses</c> and <c>Collect</c> are two readers of the same fact and must not disagree.
        /// </summary>
        /// <remarks>
        /// The per-node check exists so an applier can ask about one capability without allocating
        /// a set; that is a performance shortcut, and a shortcut that answers differently is a bug
        /// waiting for the day the report and the runtime are compared.
        /// </remarks>
        [Test]
        public void TheSingleAndBulkReadersAgree()
        {
            foreach (NexCapability capability in Enum.GetValues(typeof(NexCapability)))
            {
                if (!TryBuildNodeUsing(capability, out var node)) continue;

                var collected = new List<NexCapability>();
                NexCapabilityUse.Collect(node, collected);

                Assert.IsTrue(NexCapabilityUse.Uses(node, capability),
                    "Uses() says this node does not use " + capability + " but the node was built to.");
                Assert.That(collected, Contains.Item(capability),
                    "Collect() missed " + capability + " on a node that Uses() reports it for.");
            }
        }

        // ---- the table ------------------------------------------------------

        /// <summary>
        /// Neither backend is a subset of the other, and the table has to keep saying so.
        /// </summary>
        /// <remarks>
        /// If this ever fails it means someone has flattened the matrix to a rank - and the whole
        /// reason the compiler carries everything instead of pre-filtering to a common denominator
        /// is that the two backends genuinely lose different things.
        /// </remarks>
        [Test]
        public void NeitherBackendIsASubsetOfTheOther()
        {
            var uguiMissing = NexBackendCapabilities.MissingFrom(NexBackendId.UGui).ToArray();
            var uitkMissing = NexBackendCapabilities.MissingFrom(NexBackendId.UIToolkit).ToArray();

            Assert.IsTrue(uguiMissing.Except(uitkMissing).Any(),
                "uGUI should be missing something UI Toolkit has");
            Assert.IsTrue(uitkMissing.Except(uguiMissing).Any(),
                "UI Toolkit should be missing something uGUI has");
        }

        [Test]
        public void SupportsIsTheInverseOfTheMissingList()
        {
            foreach (var backend in NexBackendCapabilities.Backends)
            {
                var missing = new HashSet<NexCapability>(NexBackendCapabilities.MissingFrom(backend));

                foreach (NexCapability capability in Enum.GetValues(typeof(NexCapability)))
                    Assert.AreEqual(!missing.Contains(capability),
                        NexBackendCapabilities.Supports(backend, capability),
                        backend + " / " + capability);
            }
        }

        // ---- the report -----------------------------------------------------

        [Test]
        public void AScreenThatUsesNothingSpecialHasNoGaps()
        {
            var program = Screen(Node("Plain"));

            foreach (var backend in NexBackendCapabilities.Backends)
                Assert.That(NexBackendCompatibility.Analyze(program, backend), Is.Empty,
                    backend + " reported a gap for a plain panel");
        }

        /// <summary>
        /// The same screen produces different gaps per backend - the whole point of the matrix.
        /// </summary>
        [Test]
        public void OneScreenReportsDifferentGapsOnEachBackend()
        {
            var node = Node("Card");
            var appearance = NexAppearanceProgram.Neutral;
            appearance.CornerRadius = 8f;   // uGUI cannot; UI Toolkit can
            appearance.DropShadow = true;   // uGUI can; UI Toolkit cannot
            appearance.ShadowBlur = 0f;
            node.Appearance = appearance;

            var program = Screen(node);

            var ugui = NexBackendCompatibility.Analyze(program, NexBackendId.UGui)
                .Select(g => g.Capability).ToArray();
            var uitk = NexBackendCompatibility.Analyze(program, NexBackendId.UIToolkit)
                .Select(g => g.Capability).ToArray();

            Assert.That(ugui, Contains.Item(NexCapability.AppearanceCornerRadius));
            Assert.That(ugui, Has.No.Member(NexCapability.AppearanceDropShadow));
            Assert.That(uitk, Contains.Item(NexCapability.AppearanceDropShadow));
            Assert.That(uitk, Has.No.Member(NexCapability.AppearanceCornerRadius));
        }

        /// <summary>
        /// Forty slots sharing one problem are one row, not forty.
        /// </summary>
        [Test]
        public void RepeatedUseIsCountedRatherThanRepeated()
        {
            var appearance = NexAppearanceProgram.Neutral;
            appearance.CornerRadius = 8f;

            var first = Node("SlotA");
            first.Appearance = appearance;
            var second = Node("SlotB");
            second.Appearance = appearance;

            var program = Screen(first, second);

            var gaps = NexBackendCompatibility.Analyze(program, NexBackendId.UGui);

            Assert.That(gaps.Count, Is.EqualTo(1));
            Assert.That(gaps[0].NodeCount, Is.EqualTo(2));
            Assert.That(gaps[0].FirstNodePath, Is.EqualTo("SlotA"),
                "the first node is named so the author has somewhere to go and look");
        }

        [Test]
        public void AnyBackendIsCompleteAnswersForTheWholeScreen()
        {
            var node = Node("Card");
            var appearance = NexAppearanceProgram.Neutral;
            appearance.CornerRadius = 8f; // UI Toolkit renders this exactly
            node.Appearance = appearance;

            Assert.IsTrue(NexBackendCompatibility.AnyBackendIsComplete(Screen(node)),
                "a screen one backend renders exactly is shippable on that backend");
        }

        [Test]
        public void AScreenNoBackendCanRenderIsReportedAsSuch()
        {
            var node = Node("Card");
            var appearance = NexAppearanceProgram.Neutral;
            appearance.InnerShadow = true; // neither backend can draw one
            node.Appearance = appearance;

            Assert.IsFalse(NexBackendCompatibility.AnyBackendIsComplete(Screen(node)));
        }

        // ---- fixtures -------------------------------------------------------

        /// <summary>
        /// A node built to use exactly one capability, or false when none can express it.
        /// </summary>
        private static bool TryBuildNodeUsing(NexCapability capability, out NexNodeProgram node)
        {
            node = Node("Probe");
            var layout = default(NexLayoutProgram);
            var appearance = NexAppearanceProgram.Neutral;
            var type = default(NexTypographyProgram);
            type.HasOverrides = true;

            switch (capability)
            {
                case NexCapability.LayoutWrap: layout.Wrap = NexLayoutWrap.Wrap; break;
                case NexCapability.LayoutMaxSize: layout.MaxSize = new Vector2(100f, 100f); break;
                case NexCapability.LayoutAspectRatio: layout.AspectRatio = 1.5f; break;
                case NexCapability.LayoutGrid: layout.Mode = NexLayoutMode.Grid; break;
                case NexCapability.LayoutSpaceDistribution:
                    layout.Justify = NexLayoutJustify.SpaceBetween; break;
                case NexCapability.LayoutMargin: layout.Margin = new Vector4(1f, 2f, 3f, 4f); break;

                case NexCapability.AppearanceCornerRadius: appearance.CornerRadius = 8f; break;
                case NexCapability.AppearanceBorder: appearance.BorderWidth = 2f; break;
                case NexCapability.AppearanceOutline: appearance.OutlineWidth = 2f; break;
                case NexCapability.AppearanceDropShadow: appearance.DropShadow = true; break;
                case NexCapability.AppearanceShadowBlur:
                    appearance.DropShadow = true; appearance.ShadowBlur = 4f; break;
                case NexCapability.AppearanceInnerShadow: appearance.InnerShadow = true; break;
                case NexCapability.AppearanceBackgroundBlur: appearance.Blur = 4f; break;
                case NexCapability.AppearanceCrop: appearance.Crop = true; break;

                case NexCapability.TypographyAutoSize: type.AutoSize = true; break;
                case NexCapability.TypographyEllipsis: type.Ellipsis = true; break;
                case NexCapability.TypographyLineHeight: type.LineHeight = 1.5f; break;
                case NexCapability.TypographyTextShadow: type.TextShadow = true; break;
                case NexCapability.TypographyTextOutline: type.OutlineWidth = 1f; break;
                case NexCapability.TypographyRightToLeft: type.RightToLeft = true; break;
                case NexCapability.TypographyFontWeight: type.Weight = NexFontWeight.Bold; break;

                case NexCapability.StyleClasses:
                    node.Style = new NexStyleProgram { Classes = new[] { "card" } }; break;
                case NexCapability.ThemeTokens:
                    node.Style = new NexStyleProgram
                    {
                        TokenOverrides = new[] { new NexTokenOverride { Key = "accent", Value = "#fff" } }
                    };
                    break;
                case NexCapability.Motion:
                    node.Motion = new NexMotionProgram { MotionId = "motion.hover" }; break;
                case NexCapability.Localization: node.LocalizationKey = "ui.title"; break;

                default: return false;
            }

            node.Layout = layout;
            node.Appearance = appearance;
            node.Typography = type;
            return true;
        }
    }
}
