using System.Collections.Generic;
using emiteat.NexUI.Components;
using NUnit.Framework;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// The collection engine is pure C#, so its virtualization, selection and state rules are tested
    /// without a scene, a canvas or a panel - which is the point of keeping the arithmetic out of the
    /// backend adapters.
    /// </summary>
    public sealed class CollectionControllerTests
    {
        private static NXCollectionController Vertical(int count, float itemSize = 100f, float viewport = 500f,
            int overscan = 0)
        {
            var controller = new NXCollectionController
            {
                Options = new NXCollectionOptions
                {
                    Layout = NXCollectionLayout.Vertical,
                    Virtualization = NXVirtualizationMode.FixedSize,
                    ItemSize = itemSize,
                    Spacing = 0f,
                    Overscan = overscan
                }
            };
            controller.SetViewport(viewport, 300f);
            controller.SetItemCount(count);
            return controller;
        }

        [Test]
        public void VirtualizationRealizesOnlyTheVisibleWindow()
        {
            var controller = Vertical(10_000);

            var range = controller.VisibleRange;

            Assert.AreEqual(0, range.FirstIndex);
            Assert.LessOrEqual(range.Count, 8, "A 500px viewport of 100px rows must not realize thousands of items.");
            Assert.AreEqual(10_000 * 100f, controller.ContentSize);
        }

        [Test]
        public void ScrollingMovesTheRealizedWindow()
        {
            var controller = Vertical(1_000);

            controller.SetScrollOffset(10_000f);

            Assert.AreEqual(100, controller.VisibleRange.FirstIndex);
            Assert.IsTrue(controller.VisibleRange.Contains(104));
        }

        [Test]
        public void NoVirtualizationRealizesEverything()
        {
            var controller = Vertical(40);
            controller.Options = new NXCollectionOptions
            {
                Virtualization = NXVirtualizationMode.None, ItemSize = 100f, Spacing = 0f
            };

            Assert.AreEqual(new NXCollectionRange(0, 40), controller.VisibleRange);
        }

        [Test]
        public void GridDerivesLinesFromColumnCount()
        {
            var controller = new NXCollectionController
            {
                Options = new NXCollectionOptions
                {
                    Layout = NXCollectionLayout.Grid, ColumnCount = 4, ItemSize = 50f, Spacing = 0f,
                    CrossSpacing = 0f, ItemCrossSize = 50f, Overscan = 0
                }
            };
            controller.SetViewport(200f, 200f);
            controller.SetItemCount(20);

            Assert.AreEqual(4, controller.ColumnCount);
            Assert.AreEqual(5, controller.LineCount);
            Assert.AreEqual(250f, controller.ContentSize);
            Assert.AreEqual(2, controller.ColumnOf(6));
            Assert.AreEqual(50f, controller.OffsetOf(6), "Index 6 is on the second row.");
        }

        [Test]
        public void AutoColumnsFollowTheViewportWidth()
        {
            var controller = new NXCollectionController
            {
                Options = new NXCollectionOptions
                {
                    Layout = NXCollectionLayout.Grid, AutoColumns = true,
                    ItemCrossSize = 100f, CrossSpacing = 0f, ItemSize = 100f, Spacing = 0f
                }
            };
            controller.SetItemCount(32);

            controller.SetViewport(400f, 800f);
            Assert.AreEqual(8, controller.ColumnCount);

            controller.SetViewport(400f, 400f);
            Assert.AreEqual(4, controller.ColumnCount, "Narrowing the viewport must re-derive the columns.");
        }

        [Test]
        public void DynamicSizeUsesMeasurementsAndFallsBackToTheEstimate()
        {
            var controller = new NXCollectionController
            {
                Options = new NXCollectionOptions
                {
                    Virtualization = NXVirtualizationMode.DynamicSize, ItemSize = 100f, Spacing = 0f, Overscan = 0
                }
            };
            controller.SetViewport(500f, 300f);
            controller.SetItemCount(10);

            Assert.AreEqual(1000f, controller.ContentSize, "Un-measured items use the estimate.");

            controller.SetMeasuredSize(0, 300f);

            Assert.AreEqual(1200f, controller.ContentSize);
            Assert.AreEqual(300f, controller.OffsetOf(1), "The second item starts after the measured first.");
        }

        [Test]
        public void SingleSelectionReplacesAndMultipleAccumulates()
        {
            var controller = Vertical(10);
            controller.Options.Selection = NXSelectionMode.Single;

            controller.Select(2);
            controller.Select(5);
            Assert.AreEqual(new[] { 5 }, controller.SelectedIndices);

            controller.Options.Selection = NXSelectionMode.Multiple;
            controller.Select(7, additive: true);
            CollectionAssert.AreEquivalent(new[] { 5, 7 }, controller.SelectedIndices);

            controller.Select(7, additive: true);
            CollectionAssert.AreEquivalent(new[] { 5 }, controller.SelectedIndices, "Ctrl-click toggles off.");
        }

        [Test]
        public void RangeSelectionExtendsFromTheAnchor()
        {
            var controller = Vertical(10);
            controller.Options.Selection = NXSelectionMode.Multiple;

            controller.Select(2);
            controller.Select(5, rangeFromAnchor: true);

            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5 }, controller.SelectedIndices);
        }

        [Test]
        public void SelectionIsPrunedWhenTheCollectionShrinks()
        {
            var controller = Vertical(10);
            controller.Options.Selection = NXSelectionMode.Multiple;
            controller.Select(8, additive: true);
            controller.Select(9, additive: true);

            controller.SetItemCount(5);

            CollectionAssert.IsEmpty(controller.SelectedIndices,
                "A selection pointing past the end would crash the next bind.");
        }

        [Test]
        public void SelectionIsIgnoredWhenTheModeIsNone()
        {
            var controller = Vertical(10);
            controller.Options.Selection = NXSelectionMode.None;

            controller.Select(3);

            Assert.AreEqual(-1, controller.SelectedIndex);
        }

        [Test]
        public void EmptyStateIsDerivedFromTheItemCount()
        {
            var controller = Vertical(0);

            Assert.AreEqual(NXCollectionState.Empty, controller.State,
                "Content with no items is the empty state; callers should not have to special-case it.");

            controller.SetItemCount(3);
            Assert.AreEqual(NXCollectionState.Content, controller.State);

            controller.State = NXCollectionState.Error;
            Assert.AreEqual(NXCollectionState.Error, controller.State);
        }

        [Test]
        public void ScrollToBringsAnOffscreenItemIntoView()
        {
            var controller = Vertical(100);
            var requested = new List<float>();
            controller.ScrollRequested += requested.Add;

            controller.ScrollTo(50, NXScrollAlignment.Start);

            Assert.AreEqual(1, requested.Count);
            Assert.AreEqual(5000f, requested[0]);
            Assert.AreEqual(50, controller.VisibleRange.FirstIndex);
        }

        [Test]
        public void ScrollToDoesNothingWhenTheItemIsAlreadyVisible()
        {
            var controller = Vertical(100);
            var requested = 0;
            controller.ScrollRequested += _ => requested++;

            controller.ScrollTo(2);

            Assert.AreEqual(0, requested, "Nearest alignment must not jolt the view for a visible item.");
        }

        [Test]
        public void InfinitePagingAsksForMoreOncePerCount()
        {
            var controller = Vertical(50);
            controller.Options.Paging = NXPagingMode.Infinite;
            controller.Options.LoadMoreThreshold = 5;
            var requests = 0;
            controller.LoadMoreRequested += () => requests++;

            controller.SetScrollOffset(4600f);
            controller.SetScrollOffset(4700f);

            Assert.AreEqual(1, requests, "Scrolling around the end must not fire a request per frame.");

            controller.SetItemCount(100);
            controller.SetScrollOffset(9600f);
            Assert.AreEqual(2, requests, "A new page re-arms the request.");
        }

        [Test]
        public void ReorderRemapsTheSelectionAndReportsTheMove()
        {
            var controller = Vertical(10);
            controller.Options.Interactions = NXCollectionInteractions.Reorder;
            controller.Options.Selection = NXSelectionMode.Single;
            controller.Select(2);
            (int from, int to) moved = (-1, -1);
            controller.ItemMoved += (from, to) => moved = (from, to);

            Assert.IsTrue(controller.Move(2, 6));

            Assert.AreEqual((2, 6), moved);
            Assert.AreEqual(6, controller.SelectedIndex, "The selection follows the item it was on.");
        }

        [Test]
        public void ReorderIsRefusedWhenTheInteractionIsNotEnabled()
        {
            var controller = Vertical(10);
            controller.Options.Interactions = NXCollectionInteractions.Activate;

            Assert.IsFalse(controller.Move(1, 2));
        }

        [Test]
        public void ActivationRespectsTheInteractionFlags()
        {
            var controller = Vertical(10);
            controller.Options.Interactions = NXCollectionInteractions.None;
            var activated = -1;
            controller.ItemActivated += index => activated = index;

            controller.Activate(3);
            Assert.AreEqual(-1, activated);

            controller.Options.Interactions = NXCollectionInteractions.Activate;
            controller.Activate(3);
            Assert.AreEqual(3, activated);
        }

        [Test]
        public void UnsupportedOptionCombinationsAreReportedRatherThanApproximated()
        {
            var problems = new List<string>();
            var options = new NXCollectionOptions
            {
                Layout = NXCollectionLayout.Wrap,
                Virtualization = NXVirtualizationMode.DynamicSize
            };

            Assert.IsFalse(options.Validate(problems));
            Assert.IsNotEmpty(problems);
            StringAssert.Contains("Wrap", problems[0]);
        }

        [Test]
        public void SupportedOptionsValidateCleanly()
        {
            var problems = new List<string>();
            var options = new NXCollectionOptions
            {
                Layout = NXCollectionLayout.Grid, ColumnCount = 4,
                Virtualization = NXVirtualizationMode.FixedSize, Selection = NXSelectionMode.Multiple
            };

            Assert.IsTrue(options.Validate(problems), string.Join(" / ", problems));
            CollectionAssert.IsEmpty(problems);
        }

        [Test]
        public void SourceSuppliesItemsWithoutTheControllerOwningThem()
        {
            var source = new NXCollectionSource<string>(new[] { "a", "b", "c" });
            var changes = 0;
            source.Changed += () => changes++;

            Assert.AreEqual(3, source.Count);
            Assert.AreEqual("b", source.Get(1));
            Assert.AreEqual("b", source.GetItem(1));

            source.Set(new[] { "x" });
            Assert.AreEqual(1, changes);
            Assert.AreEqual(1, source.Count);
        }
    }
}
