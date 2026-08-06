using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Vector
{
    /// <summary>
    /// Combines two shapes into one - union, intersect, subtract, exclude.
    /// </summary>
    /// <remarks>
    /// <b>The result is polygonal.</b> Curves are flattened before clipping and the output is
    /// corner anchors, because solving curve-curve intersections exactly is a different and much
    /// larger problem than clipping straight edges. Every vector editor makes this same trade at
    /// some tolerance; what matters is saying so, since a designer who combines two circles and
    /// then drags a point will find corners where they expected handles.
    ///
    /// The tolerance is therefore a real parameter and not an implementation detail:
    /// <see cref="NexPathFlattening.DefaultTolerance"/> is tuned for shapes a few hundred pixels
    /// across, and artwork much smaller or much larger wants it scaled to match.
    /// </remarks>
    public static class NexVectorBoolean
    {
        /// <summary>Combines two shapes, keeping the first shape's fill and stroke settings.</summary>
        /// <param name="subject">The shape being operated on. Its appearance carries to the result.</param>
        /// <param name="clip">The shape doing the operating.</param>
        /// <param name="operation">Which parts to keep.</param>
        /// <param name="tolerance">How far the flattened path may deviate from the true curve.</param>
        /// <returns>
        /// A new shape, or an empty one when the operation leaves nothing. Never null, and never
        /// either input - a boolean operation that quietly aliased its operands would make undo
        /// restore a shape that had already been overwritten.
        /// </returns>
        public static NexVectorShape Combine(NexVectorShape subject, NexVectorShape clip,
            NexBooleanOperation operation, float tolerance = NexPathFlattening.DefaultTolerance)
        {
            var result = Appearance(subject ?? clip);

            var subjectRings = NexPathFlattening.Flatten(subject, tolerance);
            var clipRings = NexPathFlattening.Flatten(clip, tolerance);

            var rings = NexPolygonClipper.Clip(
                ToReadOnly(subjectRings), ToReadOnly(clipRings), operation);

            for (var i = 0; i < rings.Count; i++)
            {
                if (rings[i].Count < 3) continue;
                result.Contours.Add(NexPathFlattening.ToContour(rings[i]));
            }

            // Overlapping rings are how a hole is expressed, and the non-zero rule is what reads
            // the opposite winding the clipper gave them as "cut this out".
            result.FillRule = NexFillRule.NonZero;
            return result;
        }

        /// <summary>
        /// Combines a run of shapes left to right.
        /// </summary>
        /// <remarks>
        /// Sequential rather than all-at-once, which is what makes subtract behave the way it
        /// reads: <c>A - B - C</c> is A with both cut out, not A minus the union of the rest.
        /// </remarks>
        public static NexVectorShape Combine(IReadOnlyList<NexVectorShape> shapes,
            NexBooleanOperation operation, float tolerance = NexPathFlattening.DefaultTolerance)
        {
            if (shapes == null || shapes.Count == 0) return new NexVectorShape();
            if (shapes.Count == 1) return shapes[0]?.Clone() ?? new NexVectorShape();

            var accumulated = shapes[0];
            for (var i = 1; i < shapes.Count; i++)
                accumulated = Combine(accumulated, shapes[i], operation, tolerance);

            return accumulated;
        }

        /// <summary>An empty shape carrying <paramref name="source"/>'s fill and stroke settings.</summary>
        private static NexVectorShape Appearance(NexVectorShape source)
        {
            if (source == null) return new NexVectorShape();

            return new NexVectorShape
            {
                Filled = source.Filled,
                FillColor = source.FillColor,
                StrokeWidth = source.StrokeWidth,
                StrokeColor = source.StrokeColor,
                Join = source.Join,
                Cap = source.Cap
            };
        }

        private static IReadOnlyList<IReadOnlyList<Vector2>> ToReadOnly(List<List<Vector2>> rings)
        {
            var result = new List<IReadOnlyList<Vector2>>(rings.Count);
            for (var i = 0; i < rings.Count; i++) result.Add(rings[i]);
            return result;
        }
    }
}
