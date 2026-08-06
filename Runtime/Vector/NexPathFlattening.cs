using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Vector
{
    /// <summary>
    /// Turns curved contours into polygons.
    /// </summary>
    /// <remarks>
    /// Boolean operations need straight edges. Solving curve-curve intersections exactly is a
    /// root-finding problem per pair with its own degeneracies, and no drawing tool does it that
    /// way: they flatten, clip, and let the result be a polygon. That is the trade this makes too,
    /// and it is why <see cref="NexVectorBoolean"/> documents its output as corner anchors.
    ///
    /// Flattening is adaptive rather than fixed-step. A fixed count wastes points on the straight
    /// parts of a path and still visibly corners the tight parts - which on a boolean result shows
    /// up as a chewed edge exactly where two shapes meet, the one place anybody is looking.
    /// </remarks>
    public static class NexPathFlattening
    {
        /// <summary>Deviation from the true curve, in the shape's own units.</summary>
        public const float DefaultTolerance = 0.25f;

        /// <summary>
        /// Guards against a pathological curve subdividing forever.
        /// </summary>
        /// <remarks>
        /// 16 levels is 65536 segments for one curve - far past any tolerance a UI needs, so
        /// reaching it means the curve is degenerate rather than detailed.
        /// </remarks>
        private const int MaxDepth = 16;

        /// <summary>Flattens every contour of a shape. Contours of fewer than two anchors are dropped.</summary>
        public static List<List<Vector2>> Flatten(NexVectorShape shape, float tolerance = DefaultTolerance)
        {
            var result = new List<List<Vector2>>();
            if (shape == null) return result;

            for (var i = 0; i < shape.Contours.Count; i++)
            {
                var polygon = Flatten(shape.Contours[i], tolerance);
                if (polygon != null && polygon.Count >= 3) result.Add(polygon);
            }

            return result;
        }

        /// <summary>
        /// Flattens one contour into a closed ring of points.
        /// </summary>
        /// <remarks>
        /// An open contour is treated as closed. A boolean operation on an open path has no
        /// meaning - there is no inside - and closing it is what the author would have to do
        /// anyway, so it is done here rather than refused.
        /// </remarks>
        public static List<Vector2> Flatten(NexVectorContour contour, float tolerance = DefaultTolerance)
        {
            if (contour == null || contour.Anchors.Count < 2) return null;

            var anchors = contour.Anchors;
            var count = anchors.Count;
            var points = new List<Vector2>();
            var limit = Mathf.Max(1e-4f, tolerance);

            for (var i = 0; i < count; i++)
            {
                var from = anchors[i];
                var to = anchors[(i + 1) % count];

                points.Add(from.Position);

                // A segment with no handles is already a line; emitting only its endpoints keeps
                // rectangles and polygons exactly as authored rather than as dense point soup.
                if (from.OutHandle == Vector2.zero && to.InHandle == Vector2.zero) continue;

                Subdivide(points, from.Position, from.Position + from.OutHandle,
                    to.Position + to.InHandle, to.Position, limit, 0);
            }

            RemoveDuplicates(points);
            return points;
        }

        /// <summary>
        /// Emits the interior of one cubic, excluding both endpoints.
        /// </summary>
        /// <remarks>
        /// Flatness is measured as the control points' distance from the chord. That is the
        /// standard test and it is the right one here: it bounds exactly the error the caller
        /// asked about, unlike an angle or a step count which bound something correlated with it.
        /// </remarks>
        private static void Subdivide(List<Vector2> output, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            float tolerance, int depth)
        {
            if (depth >= MaxDepth || IsFlat(p0, p1, p2, p3, tolerance)) return;

            var a = (p0 + p1) * 0.5f;
            var b = (p1 + p2) * 0.5f;
            var c = (p2 + p3) * 0.5f;
            var d = (a + b) * 0.5f;
            var e = (b + c) * 0.5f;
            var middle = (d + e) * 0.5f;

            Subdivide(output, p0, a, d, middle, tolerance, depth + 1);
            output.Add(middle);
            Subdivide(output, middle, e, c, p3, tolerance, depth + 1);
        }

        private static bool IsFlat(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float tolerance)
        {
            var chord = p3 - p0;
            var lengthSquared = chord.sqrMagnitude;

            // A degenerate chord - the curve returns to where it started - has no line to measure
            // against, so fall back to how far the controls wander from the shared endpoint.
            if (lengthSquared < 1e-12f)
            {
                return (p1 - p0).sqrMagnitude <= tolerance * tolerance
                       && (p2 - p0).sqrMagnitude <= tolerance * tolerance;
            }

            // Perpendicular distance is |cross| / |chord|, so comparing against the tolerance
            // squared times the chord length squared avoids the square root on both sides.
            var limit = tolerance * tolerance * lengthSquared;
            var first = Cross(chord, p1 - p0);
            var second = Cross(chord, p2 - p0);
            return first * first <= limit && second * second <= limit;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>
        /// Drops points that repeat, including the wrap from last back to first.
        /// </summary>
        /// <remarks>
        /// Zero-length edges are what a sweep-line clipper chokes on: they have no direction, so
        /// every ordering question about them is unanswerable. Removing them here means the
        /// clipper never has to have an opinion.
        /// </remarks>
        private static void RemoveDuplicates(List<Vector2> points)
        {
            for (var i = points.Count - 1; i > 0; i--)
                if (Near(points[i], points[i - 1])) points.RemoveAt(i);

            while (points.Count > 1 && Near(points[points.Count - 1], points[0]))
                points.RemoveAt(points.Count - 1);
        }

        private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-12f;

        /// <summary>Rebuilds a contour of corner anchors from a flattened ring.</summary>
        public static NexVectorContour ToContour(IReadOnlyList<Vector2> points)
        {
            var contour = new NexVectorContour { Closed = true };
            if (points == null) return contour;

            for (var i = 0; i < points.Count; i++) contour.Anchors.Add(new NexVectorAnchor(points[i]));
            return contour;
        }

        /// <summary>Twice the signed area. Positive is counter-clockwise in a y-up reading.</summary>
        /// <remarks>
        /// Used for winding, where only the sign matters, so the factor of two is left in rather
        /// than divided out to keep it exact.
        /// </remarks>
        public static float SignedDoubleArea(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0f;

            var total = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                total += current.x * next.y - next.x * current.y;
            }

            return total;
        }
    }
}
