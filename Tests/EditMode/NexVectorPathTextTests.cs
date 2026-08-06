using emiteat.NexUI.Vector;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// The path's text form, which is how a shape survives in a generated UXML file.
    /// </summary>
    /// <remarks>
    /// Checked by round trip rather than by comparing strings. There are many correct ways to write
    /// the same path - spacing, decimal places, a line as a degenerate curve - and pinning one of
    /// them would turn a harmless formatting change into a failure while still missing the thing
    /// that matters: that reading it back gives the same shape.
    /// </remarks>
    public sealed class NexVectorPathTextTests
    {
        private static void AssertClose(Vector2 expected, Vector2 actual, string because)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, because + " (x)");
            Assert.AreEqual(expected.y, actual.y, 1e-3f, because + " (y)");
        }

        private static NexVectorShape RoundTrip(NexVectorShape shape)
            => NexVectorPathText.Decode(NexVectorPathText.Encode(shape));

        [Test]
        public void ARectangleSurvivesTheRoundTrip()
        {
            var original = NexShapeFactory.Rectangle(new Rect(10f, 20f, 100f, 50f));
            var restored = RoundTrip(original);

            Assert.AreEqual(1, restored.Contours.Count);
            Assert.AreEqual(4, restored.Contours[0].Anchors.Count,
                "a rectangle must come back as four corners, not five with a duplicate");
            Assert.IsTrue(restored.Contours[0].Closed);

            var bounds = restored.Bounds();
            AssertClose(new Vector2(10f, 20f), bounds.min, "position");
            AssertClose(new Vector2(100f, 50f), bounds.size, "size");
        }

        [Test]
        public void CurvesKeepTheirHandles()
        {
            var original = NexShapeFactory.Ellipse(new Rect(0f, 0f, 80f, 40f));
            var restored = RoundTrip(original);

            Assert.AreEqual(original.Contours[0].Anchors.Count, restored.Contours[0].Anchors.Count);

            for (var i = 0; i < original.Contours[0].Anchors.Count; i++)
            {
                var before = original.Contours[0].Anchors[i];
                var after = restored.Contours[0].Anchors[i];

                AssertClose(before.Position, after.Position, "anchor " + i + " position");
                AssertClose(before.InHandle, after.InHandle, "anchor " + i + " in handle");
                AssertClose(before.OutHandle, after.OutHandle, "anchor " + i + " out handle");
            }
        }

        [Test]
        public void CornersStayCorners()
        {
            // A straight segment is written as L rather than as a cubic with redundant controls,
            // and must not come back carrying handles it never had.
            var restored = RoundTrip(NexShapeFactory.Polygon(new Rect(0f, 0f, 60f, 60f), 5));

            foreach (var anchor in restored.Contours[0].Anchors)
                Assert.IsTrue(anchor.IsCorner, "a polygon's points must stay hard corners");
        }

        [Test]
        public void CompoundPathsKeepEveryContour()
        {
            // What a hole is made of - losing a contour would silently fill in the hole.
            var shape = new NexVectorShape();
            shape.Contours.Add(NexShapeFactory.Rectangle(new Rect(0f, 0f, 100f, 100f)).Contours[0]);
            shape.Contours.Add(NexShapeFactory.Rectangle(new Rect(25f, 25f, 50f, 50f)).Contours[0]);

            var restored = RoundTrip(shape);

            Assert.AreEqual(2, restored.Contours.Count, "both rings must survive");
            Assert.AreEqual(4, restored.Contours[1].Anchors.Count);
        }

        [Test]
        public void AnOpenPathStaysOpen()
        {
            var shape = new NexVectorShape();
            shape.Contours.Add(new NexVectorContour(new[]
            {
                new NexVectorAnchor(new Vector2(0f, 0f)),
                new NexVectorAnchor(new Vector2(10f, 10f)),
                new NexVectorAnchor(new Vector2(20f, 0f))
            }, closed: false));

            var restored = RoundTrip(shape);

            Assert.IsFalse(restored.Contours[0].Closed, "an open run must not be closed by the round trip");
            Assert.AreEqual(3, restored.Contours[0].Anchors.Count);
        }

        [Test]
        public void ARepeatedClosingPointIsDropped()
        {
            // Text written by another tool often repeats the first point before Z. Keeping it would
            // leave an anchor sitting exactly on another for the pen tool to snag on forever.
            var restored = NexVectorPathText.Decode("M 0 0 L 10 0 L 10 10 L 0 0 Z");

            Assert.AreEqual(3, restored.Contours[0].Anchors.Count,
                "the duplicated closing point must be merged away");
            Assert.IsTrue(restored.Contours[0].Closed);
        }

        [Test]
        public void CommasAndTightSyntaxParse()
        {
            // Both are ordinary in SVG written by other tools: commas as separators, and negative
            // numbers running straight into the previous one with no space.
            var restored = NexVectorPathText.Decode("M0,0L10,0L10,10Z");

            Assert.AreEqual(3, restored.Contours[0].Anchors.Count);
            AssertClose(new Vector2(10f, 10f), restored.Contours[0].Anchors[2].Position, "third point");
        }

        [Test]
        public void NegativeCoordinatesWithoutSeparatorsParse()
        {
            var restored = NexVectorPathText.Decode("M-5-5L5-5L5 5Z");

            Assert.AreEqual(3, restored.Contours[0].Anchors.Count, "the run-together minus signs must split");
            AssertClose(new Vector2(-5f, -5f), restored.Contours[0].Anchors[0].Position, "first point");
        }

        [Test]
        public void GarbageDoesNotThrow()
        {
            // This runs while a UXML document is loading, where an exception takes down the whole
            // file and a missing shape takes down one element.
            Assert.DoesNotThrow(() => NexVectorPathText.Decode("M nonsense L"));
            Assert.DoesNotThrow(() => NexVectorPathText.Decode("Q 1 2 3 4"));
            Assert.DoesNotThrow(() => NexVectorPathText.Decode("Z Z Z"));
        }

        [Test]
        public void EmptyInputsGiveEmptyResults()
        {
            Assert.AreEqual(string.Empty, NexVectorPathText.Encode(null));
            Assert.IsTrue(NexVectorPathText.Decode(null).IsEmpty);
            Assert.IsTrue(NexVectorPathText.Decode("   ").IsEmpty);
        }

        [Test]
        public void CultureDoesNotChangeTheOutput()
        {
            // A decimal comma would produce "10,5" and make the number a separator - the classic
            // way a serializer breaks only on some machines.
            var previous = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture =
                    new System.Globalization.CultureInfo("de-DE");

                var restored = RoundTrip(NexShapeFactory.Rectangle(new Rect(0.5f, 1.25f, 10f, 10f)));
                AssertClose(new Vector2(0.5f, 1.25f), restored.Bounds().min,
                    "fractional coordinates must survive a comma-decimal culture");
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
