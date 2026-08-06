using UnityEngine;

namespace emiteat.NexUI.Vector
{
    /// <summary>
    /// Builds the shapes a design tool is expected to have, as ordinary paths.
    /// </summary>
    /// <remarks>
    /// Every one of these produces a <see cref="NexVectorShape"/> rather than a special-cased
    /// component. That is the whole point: a star the pen tool can then edit is worth more than a
    /// star with a "points" slider and no way in, and it means one renderer draws all of them.
    ///
    /// Sizes are given as a rect so a preset drops into the element's bounds the way the rest of
    /// the Designer works, instead of asking the author for a radius in unrelated units.
    /// </remarks>
    public static class NexShapeFactory
    {
        /// <summary>Circular arc handle length for a quarter turn - the standard bezier constant.</summary>
        /// <remarks>
        /// 4/3·tan(θ/4) with θ = 90°. Approximating a circle with cubic beziers is exact enough
        /// at this constant that the error is well under a pixel at UI sizes, which is why every
        /// vector format uses it rather than tessellating circles as polygons.
        /// </remarks>
        private const float CircleHandle = 0.5522847498f;

        public static NexVectorShape Rectangle(Rect rect)
        {
            var contour = new NexVectorContour();
            contour.Anchors.Add(new NexVectorAnchor(new Vector2(rect.xMin, rect.yMin)));
            contour.Anchors.Add(new NexVectorAnchor(new Vector2(rect.xMax, rect.yMin)));
            contour.Anchors.Add(new NexVectorAnchor(new Vector2(rect.xMax, rect.yMax)));
            contour.Anchors.Add(new NexVectorAnchor(new Vector2(rect.xMin, rect.yMax)));
            return Wrap(contour);
        }

        public static NexVectorShape Ellipse(Rect rect)
        {
            var centre = rect.center;
            var rx = rect.width * 0.5f;
            var ry = rect.height * 0.5f;
            var hx = rx * CircleHandle;
            var hy = ry * CircleHandle;

            var contour = new NexVectorContour();
            contour.Anchors.Add(new NexVectorAnchor(new Vector2(centre.x, centre.y - ry),
                new Vector2(hx, 0f), new Vector2(-hx, 0f)));
            contour.Anchors.Add(new NexVectorAnchor(new Vector2(centre.x - rx, centre.y),
                new Vector2(0f, -hy), new Vector2(0f, hy)));
            contour.Anchors.Add(new NexVectorAnchor(new Vector2(centre.x, centre.y + ry),
                new Vector2(-hx, 0f), new Vector2(hx, 0f)));
            contour.Anchors.Add(new NexVectorAnchor(new Vector2(centre.x + rx, centre.y),
                new Vector2(0f, hy), new Vector2(0f, -hy)));
            return Wrap(contour);
        }

        /// <summary>Regular polygon inscribed in <paramref name="rect"/>.</summary>
        public static NexVectorShape Polygon(Rect rect, int sides, float rotationDegrees = -90f)
        {
            sides = Mathf.Max(3, sides);

            var centre = rect.center;
            var rx = rect.width * 0.5f;
            var ry = rect.height * 0.5f;

            var contour = new NexVectorContour();
            for (var i = 0; i < sides; i++)
            {
                var angle = (rotationDegrees + 360f * i / sides) * Mathf.Deg2Rad;
                contour.Anchors.Add(new NexVectorAnchor(
                    new Vector2(centre.x + Mathf.Cos(angle) * rx, centre.y + Mathf.Sin(angle) * ry)));
            }
            return Wrap(contour);
        }

        /// <summary>
        /// Star with <paramref name="points"/> tips.
        /// </summary>
        /// <param name="innerRatio">Inner radius as a fraction of the outer one.</param>
        public static NexVectorShape Star(Rect rect, int points, float innerRatio = 0.5f,
            float rotationDegrees = -90f)
        {
            points = Mathf.Max(3, points);
            innerRatio = Mathf.Clamp(innerRatio, 0.01f, 1f);

            var centre = rect.center;
            var rx = rect.width * 0.5f;
            var ry = rect.height * 0.5f;

            var contour = new NexVectorContour();
            for (var i = 0; i < points * 2; i++)
            {
                var outer = (i & 1) == 0;
                var scale = outer ? 1f : innerRatio;
                var angle = (rotationDegrees + 180f * i / points) * Mathf.Deg2Rad;
                contour.Anchors.Add(new NexVectorAnchor(new Vector2(
                    centre.x + Mathf.Cos(angle) * rx * scale,
                    centre.y + Mathf.Sin(angle) * ry * scale)));
            }
            return Wrap(contour);
        }

        /// <summary>
        /// Ring, or a slice of one when <paramref name="sweepDegrees"/> is less than a full turn.
        /// </summary>
        /// <remarks>
        /// A ring is one contour that walks the outer edge and comes back along the inner one,
        /// rather than two contours plus a fill rule. Both work for drawing; a single contour also
        /// strokes correctly, which two would not - the stroke would trace each circle separately
        /// instead of following the ring's actual outline.
        /// </remarks>
        public static NexVectorShape Ring(Rect rect, float thickness, float startDegrees = 0f,
            float sweepDegrees = 360f, int segments = 48)
        {
            var centre = rect.center;
            var outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            var inner = Mathf.Clamp(outer - Mathf.Max(0f, thickness), 0f, outer);

            var full = Mathf.Abs(sweepDegrees) >= 360f;
            var sweep = full ? 360f : Mathf.Clamp(sweepDegrees, -360f, 360f);
            var steps = Mathf.Max(3, Mathf.CeilToInt(segments * Mathf.Abs(sweep) / 360f));

            var contour = new NexVectorContour();

            for (var i = 0; i <= steps; i++)
            {
                var angle = (startDegrees + sweep * i / steps) * Mathf.Deg2Rad;
                contour.Anchors.Add(new NexVectorAnchor(
                    new Vector2(centre.x + Mathf.Cos(angle) * outer, centre.y + Mathf.Sin(angle) * outer)));
            }

            for (var i = steps; i >= 0; i--)
            {
                var angle = (startDegrees + sweep * i / steps) * Mathf.Deg2Rad;
                contour.Anchors.Add(new NexVectorAnchor(
                    new Vector2(centre.x + Mathf.Cos(angle) * inner, centre.y + Mathf.Sin(angle) * inner)));
            }

            return Wrap(contour);
        }

        /// <summary>Pie slice - a ring whose inner radius is zero.</summary>
        public static NexVectorShape Pie(Rect rect, float startDegrees, float sweepDegrees, int segments = 48)
        {
            var outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            return Ring(rect, outer, startDegrees, sweepDegrees, segments);
        }

        /// <summary>Open arc, meant to be stroked rather than filled.</summary>
        public static NexVectorShape Arc(Rect rect, float startDegrees, float sweepDegrees,
            float strokeWidth, int segments = 48)
        {
            var centre = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var steps = Mathf.Max(2, Mathf.CeilToInt(segments * Mathf.Abs(sweepDegrees) / 360f));

            var contour = new NexVectorContour { Closed = false };
            for (var i = 0; i <= steps; i++)
            {
                var angle = (startDegrees + sweepDegrees * i / steps) * Mathf.Deg2Rad;
                contour.Anchors.Add(new NexVectorAnchor(
                    new Vector2(centre.x + Mathf.Cos(angle) * radius, centre.y + Mathf.Sin(angle) * radius)));
            }

            var shape = Wrap(contour);
            shape.Filled = false;
            shape.StrokeWidth = strokeWidth;
            return shape;
        }

        /// <summary>Rounded rectangle, as a real path rather than a shader trick.</summary>
        public static NexVectorShape RoundedRectangle(Rect rect, float radius)
        {
            var limit = Mathf.Min(rect.width, rect.height) * 0.5f;
            radius = Mathf.Clamp(radius, 0f, limit);
            if (radius <= 0f) return Rectangle(rect);

            var handle = radius * CircleHandle;
            var contour = new NexVectorContour();

            // Corner order matches Rectangle so a radius animating to zero lands on the same path.
            AddCorner(contour, new Vector2(rect.xMin + radius, rect.yMin), new Vector2(-handle, 0f), Vector2.zero);
            AddCorner(contour, new Vector2(rect.xMax - radius, rect.yMin), Vector2.zero, new Vector2(handle, 0f));
            AddCorner(contour, new Vector2(rect.xMax, rect.yMin + radius), new Vector2(0f, -handle), Vector2.zero);
            AddCorner(contour, new Vector2(rect.xMax, rect.yMax - radius), Vector2.zero, new Vector2(0f, handle));
            AddCorner(contour, new Vector2(rect.xMax - radius, rect.yMax), new Vector2(handle, 0f), Vector2.zero);
            AddCorner(contour, new Vector2(rect.xMin + radius, rect.yMax), Vector2.zero, new Vector2(-handle, 0f));
            AddCorner(contour, new Vector2(rect.xMin, rect.yMax - radius), new Vector2(0f, handle), Vector2.zero);
            AddCorner(contour, new Vector2(rect.xMin, rect.yMin + radius), Vector2.zero, new Vector2(0f, -handle));

            return Wrap(contour);
        }

        private static void AddCorner(NexVectorContour contour, Vector2 position, Vector2 inHandle, Vector2 outHandle)
            => contour.Anchors.Add(new NexVectorAnchor(position, inHandle, outHandle));

        private static NexVectorShape Wrap(NexVectorContour contour)
        {
            var shape = new NexVectorShape();
            shape.Contours.Add(contour);
            return shape;
        }
    }
}
