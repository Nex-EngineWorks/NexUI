using System.Collections.Generic;
using emiteat.NexUI.Vector;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// Boolean operations are checked by area and containment rather than by exact vertex lists.
    /// </summary>
    /// <remarks>
    /// A clipper is free to start a ring at any vertex, split an edge that a collinear neighbour
    /// touched, or emit an extra collinear point - all of which describe the same region. Asserting
    /// on the vertex list would make those legal outcomes look like failures and, worse, would make
    /// a genuinely wrong result pass whenever it happened to match. Area and point-in-polygon are
    /// what the operation actually promises.
    /// </remarks>
    public sealed class NexVectorBooleanTests
    {
        private const float AreaTolerance = 0.5f;

        private static NexVectorShape Rect(float x, float y, float width, float height)
        {
            var shape = new NexVectorShape();
            shape.Contours.Add(new NexVectorContour(new[]
            {
                new NexVectorAnchor(new Vector2(x, y)),
                new NexVectorAnchor(new Vector2(x + width, y)),
                new NexVectorAnchor(new Vector2(x + width, y + height)),
                new NexVectorAnchor(new Vector2(x, y + height))
            }));
            return shape;
        }

        /// <summary>Total signed area, so a hole subtracts from the ring that contains it.</summary>
        private static float Area(NexVectorShape shape)
        {
            var total = 0f;
            foreach (var contour in shape.Contours)
            {
                var points = new List<Vector2>();
                foreach (var anchor in contour.Anchors) points.Add(anchor.Position);
                total += NexPathFlattening.SignedDoubleArea(points);
            }
            return Mathf.Abs(total) * 0.5f;
        }

        private static bool Contains(NexVectorShape shape, Vector2 point)
        {
            // Non-zero winding, matching what the shape declares and what the renderer applies.
            var winding = 0;

            foreach (var contour in shape.Contours)
            {
                var anchors = contour.Anchors;
                for (var i = 0; i < anchors.Count; i++)
                {
                    var a = anchors[i].Position;
                    var b = anchors[(i + 1) % anchors.Count].Position;

                    if (a.y <= point.y)
                    {
                        if (b.y > point.y && Cross(a, b, point) > 0f) winding++;
                    }
                    else if (b.y <= point.y && Cross(a, b, point) < 0f) winding--;
                }
            }

            return winding != 0;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 p)
            => (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y);

        // ---- disjoint and identical -----------------------------------------

        [Test]
        public void UnionOfSeparateRectanglesKeepsBoth()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(20, 0, 10, 10),
                NexBooleanOperation.Union);

            Assert.AreEqual(200f, Area(result), AreaTolerance, "both rectangles must survive whole");
            Assert.IsTrue(Contains(result, new Vector2(5f, 5f)), "the first rectangle is still filled");
            Assert.IsTrue(Contains(result, new Vector2(25f, 5f)), "the second rectangle is still filled");
            Assert.IsFalse(Contains(result, new Vector2(15f, 5f)), "the gap between them must stay empty");
        }

        [Test]
        public void IntersectingSeparateRectanglesGivesNothing()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(20, 0, 10, 10),
                NexBooleanOperation.Intersect);

            Assert.AreEqual(0f, Area(result), AreaTolerance, "shapes that never touch share no area");
        }

        [Test]
        public void SubtractingAShapeFromItselfLeavesNothing()
        {
            // Every edge is collinear with its counterpart - the degenerate case that a
            // vertex-pairing clipper cannot answer at all.
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(0, 0, 10, 10),
                NexBooleanOperation.Subtract);

            Assert.AreEqual(0f, Area(result), AreaTolerance, "a shape minus itself is empty");
        }

        [Test]
        public void UnionOfIdenticalShapesIsOneOfThem()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(0, 0, 10, 10),
                NexBooleanOperation.Union);

            Assert.AreEqual(100f, Area(result), AreaTolerance, "overlapping identical shapes must not double");
        }

        // ---- partial overlap -------------------------------------------------

        [Test]
        public void UnionOfOverlappingRectanglesCountsTheOverlapOnce()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(5, 0, 10, 10),
                NexBooleanOperation.Union);

            Assert.AreEqual(150f, Area(result), AreaTolerance, "100 + 100 - 50 of shared area");
        }

        [Test]
        public void IntersectionOfOverlappingRectanglesIsTheSharedPart()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(5, 0, 10, 10),
                NexBooleanOperation.Intersect);

            Assert.AreEqual(50f, Area(result), AreaTolerance, "only the shared strip remains");
            Assert.IsTrue(Contains(result, new Vector2(7f, 5f)), "a point in the overlap is kept");
            Assert.IsFalse(Contains(result, new Vector2(2f, 5f)), "a point only the first covers is dropped");
        }

        [Test]
        public void SubtractionRemovesOnlyTheOverlap()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(5, 0, 10, 10),
                NexBooleanOperation.Subtract);

            Assert.AreEqual(50f, Area(result), AreaTolerance, "the bitten-out half is gone");
            Assert.IsTrue(Contains(result, new Vector2(2f, 5f)), "the untouched half is kept");
            Assert.IsFalse(Contains(result, new Vector2(7f, 5f)), "the overlap is removed");
        }

        [Test]
        public void ExcludeKeepsWhatOnlyOneShapeCovers()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(5, 0, 10, 10),
                NexBooleanOperation.Exclude);

            Assert.AreEqual(100f, Area(result), AreaTolerance, "both non-shared halves, and neither overlap");
            Assert.IsFalse(Contains(result, new Vector2(7f, 5f)), "the overlap must be excluded");
            Assert.IsTrue(Contains(result, new Vector2(2f, 5f)), "the first shape's own half is kept");
            Assert.IsTrue(Contains(result, new Vector2(12f, 5f)), "the second shape's own half is kept");
        }

        // ---- holes -----------------------------------------------------------

        [Test]
        public void SubtractingTheMiddleMakesAHole()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 30, 30), Rect(10, 10, 10, 10),
                NexBooleanOperation.Subtract);

            Assert.AreEqual(2, result.Contours.Count, "a frame is an outer ring plus a hole");
            Assert.AreEqual(800f, Area(result), AreaTolerance, "900 minus the 100 cut out");
            Assert.IsFalse(Contains(result, new Vector2(15f, 15f)), "the hole must not be filled");
            Assert.IsTrue(Contains(result, new Vector2(5f, 15f)), "the frame around it must be");
        }

        [Test]
        public void AHoleWindsOppositeItsOuterRing()
        {
            // What actually makes the hole render as a hole under the non-zero rule.
            var result = NexVectorBoolean.Combine(Rect(0, 0, 30, 30), Rect(10, 10, 10, 10),
                NexBooleanOperation.Subtract);

            Assert.AreEqual(NexFillRule.NonZero, result.FillRule, "the result declares non-zero fill");

            var outer = new List<Vector2>();
            foreach (var anchor in result.Contours[0].Anchors) outer.Add(anchor.Position);
            var inner = new List<Vector2>();
            foreach (var anchor in result.Contours[1].Anchors) inner.Add(anchor.Position);

            Assert.AreNotEqual(
                NexPathFlattening.SignedDoubleArea(outer) > 0f,
                NexPathFlattening.SignedDoubleArea(inner) > 0f,
                "the hole must wind against the ring containing it");
        }

        // ---- shared edges ----------------------------------------------------

        [Test]
        public void RectanglesSharingAnEdgeUniteIntoOne()
        {
            // Snapping produces this constantly, and it is exactly where a clipper that only
            // handles proper crossings falls over.
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(10, 0, 10, 10),
                NexBooleanOperation.Union);

            Assert.AreEqual(200f, Area(result), AreaTolerance, "flush rectangles keep their full area");
            Assert.IsTrue(Contains(result, new Vector2(10f, 5f)), "the seam between them is inside the result");
        }

        [Test]
        public void RectanglesSharingOnlyAnEdgeIntersectToNothing()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(10, 0, 10, 10),
                NexBooleanOperation.Intersect);

            Assert.AreEqual(0f, Area(result), AreaTolerance, "a shared edge has no area");
        }

        [Test]
        public void RectanglesTouchingAtOneCornerUniteWithoutMerging()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), Rect(10, 10, 10, 10),
                NexBooleanOperation.Union);

            Assert.AreEqual(200f, Area(result), AreaTolerance, "a single shared corner adds no area either way");
        }

        // ---- empty operands --------------------------------------------------

        [Test]
        public void CombiningWithNothingLeavesTheShapeAlone()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), new NexVectorShape(),
                NexBooleanOperation.Subtract);

            Assert.AreEqual(100f, Area(result), AreaTolerance, "subtracting nothing removes nothing");
        }

        [Test]
        public void IntersectingWithNothingGivesNothing()
        {
            var result = NexVectorBoolean.Combine(Rect(0, 0, 10, 10), null, NexBooleanOperation.Intersect);
            Assert.AreEqual(0f, Area(result), AreaTolerance, "nothing is shared with an absent shape");
        }

        [Test]
        public void TheResultNeverAliasesItsOperands()
        {
            // Undo restores the operands; if the result shared their contour objects, undoing a
            // boolean operation would hand back a shape that had already been rewritten.
            var subject = Rect(0, 0, 10, 10);
            var result = NexVectorBoolean.Combine(subject, Rect(20, 20, 5, 5), NexBooleanOperation.Union);

            Assert.AreNotSame(subject, result, "the result must be a new shape");
            foreach (var contour in result.Contours)
                Assert.AreNotSame(subject.Contours[0], contour, "and must not reuse an operand's contours");
        }

        [Test]
        public void TheResultKeepsTheSubjectsAppearance()
        {
            var subject = Rect(0, 0, 10, 10);
            subject.FillColor = Color.magenta;
            subject.StrokeWidth = 3f;

            var result = NexVectorBoolean.Combine(subject, Rect(5, 5, 10, 10), NexBooleanOperation.Union);

            Assert.AreEqual(Color.magenta, result.FillColor, "the combined shape keeps the subject's fill");
            Assert.AreEqual(3f, result.StrokeWidth, "and its stroke");
        }

        // ---- sequences -------------------------------------------------------

        [Test]
        public void SubtractingASequenceCutsEachInTurn()
        {
            var result = NexVectorBoolean.Combine(
                new[] { Rect(0, 0, 30, 10), Rect(0, 0, 10, 10), Rect(20, 0, 10, 10) },
                NexBooleanOperation.Subtract);

            Assert.AreEqual(100f, Area(result), AreaTolerance, "300 minus two 100s taken one after another");
            Assert.IsTrue(Contains(result, new Vector2(15f, 5f)), "the untouched middle survives");
        }

        // ---- curves ----------------------------------------------------------

        [Test]
        public void FlatteningUndercutsTheCurveAndTightensWithTolerance()
        {
            // A flattened curve is an inscribed polygon, so its area is always *below* the true
            // one - never above. That direction is the guarantee; the size of the gap is a
            // function of the tolerance, so the two are asserted separately.
            var circle = NexShapeFactory.Ellipse(new Rect(-10f, -10f, 20f, 20f));
            var exact = Mathf.PI * 100f;

            var coarse = Area(NexVectorBoolean.Combine(
                circle, new NexVectorShape(), NexBooleanOperation.Union));
            var fine = Area(NexVectorBoolean.Combine(
                circle, new NexVectorShape(), NexBooleanOperation.Union, tolerance: 0.005f));

            Assert.Less(coarse, exact + 0.01f, "flattening must never inflate the shape");
            Assert.Less(fine, exact + 0.01f, "nor at a finer tolerance");

            // The default is tuned for shapes a few hundred pixels across, where a percent or two
            // of area is invisible. Pinning it tighter would be asserting a tolerance nobody chose.
            Assert.Greater(coarse, exact * 0.97f, "the default must stay within a few percent");

            Assert.Greater(fine, coarse,
                "a finer tolerance must get closer to the curve, not merely differ from it");
            Assert.Greater(fine, exact * 0.999f, "and close enough to be exact for any UI purpose");
        }

        [Test]
        public void TwoOverlappingCirclesIntersectToALens()
        {
            var left = NexShapeFactory.Ellipse(new Rect(-10f, -10f, 20f, 20f));
            var right = NexShapeFactory.Ellipse(new Rect(0f, -10f, 20f, 20f));

            // Tolerance tightened for the assertion. A lens is bounded by two arcs and nothing
            // else, so unlike the rectangle cases every edge of the answer carries the flattening
            // error - and at the default tolerance that is several percent, which says more about
            // the tolerance than about the clipper this test is for.
            var result = NexVectorBoolean.Combine(left, right, NexBooleanOperation.Intersect,
                tolerance: 0.005f);

            // Two circles of radius r at distance d = r overlap in r^2*(2*pi/3 - sqrt(3)/2).
            var expected = 100f * (2f * Mathf.PI / 3f - Mathf.Sqrt(3f) / 2f);
            Assert.AreEqual(expected, Area(result), expected * 0.01f, "the overlap is the classic lens");
        }
    }
}
