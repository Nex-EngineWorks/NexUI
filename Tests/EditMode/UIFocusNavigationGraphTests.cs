using NUnit.Framework;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Tests.EditMode
{
    public sealed class UIFocusNavigationGraphTests
    {
        [Test]
        public void Resolve_FollowsExplicitLink()
        {
            var graph = new UIFocusNavigationGraph();
            graph.SetLinks(new[]
            {
                new UIFocusLink { elementId = "a", rightElementId = "b" },
                new UIFocusLink { elementId = "b", leftElementId = "a" }
            });

            Assert.AreEqual("b", graph.Resolve("a", UIFocusDirection.Right));
            Assert.AreEqual("a", graph.Resolve("b", UIFocusDirection.Left));
        }

        [Test]
        public void Resolve_NoLinkInThatDirection_ReturnsNull()
        {
            var graph = new UIFocusNavigationGraph();
            graph.SetLinks(new[] { new UIFocusLink { elementId = "a", rightElementId = "b" } });

            Assert.IsNull(graph.Resolve("a", UIFocusDirection.Up));
        }

        [Test]
        public void Resolve_UnknownElement_ReturnsNull()
        {
            var graph = new UIFocusNavigationGraph();
            graph.SetLinks(new[] { new UIFocusLink { elementId = "a", rightElementId = "b" } });

            Assert.IsNull(graph.Resolve("does-not-exist", UIFocusDirection.Right));
        }

        [Test]
        public void SetLinks_ReplacesPreviousLinksEntirely()
        {
            var graph = new UIFocusNavigationGraph();
            graph.SetLinks(new[] { new UIFocusLink { elementId = "a", rightElementId = "b" } });
            graph.SetLinks(new[] { new UIFocusLink { elementId = "a", rightElementId = "c" } });

            Assert.AreEqual("c", graph.Resolve("a", UIFocusDirection.Right));
        }

        [Test]
        public void FindUnreachableFrom_ReturnsElementsNotConnectedToDefault()
        {
            var graph = new UIFocusNavigationGraph();
            graph.SetLinks(new[]
            {
                new UIFocusLink { elementId = "a", rightElementId = "b" },
                new UIFocusLink { elementId = "b", leftElementId = "a" },
                new UIFocusLink { elementId = "isolated" } // no links to/from anything
            }, defaultElementId: "a");

            var unreachable = graph.FindUnreachableFrom("a");

            CollectionAssert.Contains(unreachable, "isolated");
            CollectionAssert.DoesNotContain(unreachable, "a");
            CollectionAssert.DoesNotContain(unreachable, "b");
        }

        [Test]
        public void Clear_RemovesAllLinksAndDefault()
        {
            var graph = new UIFocusNavigationGraph();
            graph.SetLinks(new[] { new UIFocusLink { elementId = "a", rightElementId = "b" } }, "a");
            graph.Clear();

            Assert.IsNull(graph.Resolve("a", UIFocusDirection.Right));
            Assert.IsNull(graph.DefaultElementId);
        }
    }
}
