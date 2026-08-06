using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if NEXUI_VECTOR_GRAPHICS
using Unity.VectorGraphics;
#endif

namespace emiteat.NexUI.Vector
{
    /// <summary>What an SVG import produced.</summary>
    public struct NexSvgImportResult
    {
        /// <summary>Shapes in document order, already flattened into element space.</summary>
        public List<NexVectorShape> Shapes;

        /// <summary>Combined bounds of everything imported, in the SVG's own coordinates.</summary>
        public Rect Bounds;

        /// <summary>Set when the file could not be read. Shapes is empty in that case.</summary>
        public string Error;

        public bool Succeeded => string.IsNullOrEmpty(Error);
    }

    /// <summary>
    /// Reads an SVG into NexUI paths.
    /// </summary>
    /// <remarks>
    /// Unity's built-in <see cref="SVGParser"/> does the parsing, so this is a translation rather
    /// than a parser: its scene graph carries transforms and absolute control points, and NexUI's
    /// model carries flat shapes with anchor-relative handles because that is what a pen tool
    /// edits. Flattening the hierarchy here means an imported icon behaves exactly like a drawn
    /// one - it can be selected, nudged and re-pointed with no import-only special case.
    ///
    /// Gradients, patterns, text, clipping and masks are not carried across. Each needs a NexUI
    /// concept that does not exist yet, and inventing a partial one per feature is how an importer
    /// ends up producing shapes nothing downstream can edit. Solid fills and strokes cover icons,
    /// which is what a UI actually imports.
    ///
    /// <para><b>Availability.</b> The parser ships with the same module as the tessellator, so an
    /// import on a build without it fails with <see cref="NexVectorTessellator.UnsupportedReason"/>
    /// rather than the project failing to compile - see
    /// <see cref="NexVectorTessellator.IsSupported"/>.</para>
    /// </remarks>
    public static class NexSvgImporter
    {
        /// <summary>Imports from SVG markup.</summary>
        public static NexSvgImportResult Import(string svgText)
        {
            if (!NexVectorTessellator.IsSupported)
                return Failed(NexVectorTessellator.UnsupportedReason);

            if (string.IsNullOrWhiteSpace(svgText))
                return Failed("The SVG is empty.");

#if NEXUI_VECTOR_GRAPHICS
            try
            {
                using var reader = new StringReader(svgText);

                // Zero DPI and 1 pixels-per-unit keep the SVG's own coordinates, which is what the
                // shape bounds are then fitted from. Letting the parser scale here would bake a
                // size into paths that the layout is about to decide anyway.
                var info = SVGParser.ImportSVG(reader, ViewportOptions.PreserveViewport, 0f, 1f, 100, 100);
                return FromScene(info.Scene);
            }
            catch (Exception exception)
            {
                // Malformed SVG is a normal thing to be handed, not an exceptional one. The caller
                // gets a message to show rather than a stack trace to swallow.
                return Failed(exception.Message);
            }
#else
            return Failed(NexVectorTessellator.UnsupportedReason);
#endif
        }

        /// <summary>Imports from a file on disk.</summary>
        public static NexSvgImportResult ImportFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Failed("No SVG file at '" + (path ?? "<null>") + "'.");

            try
            {
                return Import(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                return Failed(exception.Message);
            }
        }

#if NEXUI_VECTOR_GRAPHICS
        private static NexSvgImportResult FromScene(Scene scene)
        {
            var shapes = new List<NexVectorShape>();
            if (scene?.Root != null) Collect(scene.Root, Matrix2D.identity, shapes);

            if (shapes.Count == 0) return Failed("The SVG contains no shapes NexUI can import.");

            var bounds = shapes[0].Bounds();
            for (var i = 1; i < shapes.Count; i++)
            {
                var next = shapes[i].Bounds();
                var min = UnityEngine.Vector2.Min(bounds.min, next.min);
                var max = UnityEngine.Vector2.Max(bounds.max, next.max);
                bounds = new Rect(min, max - min);
            }

            return new NexSvgImportResult { Shapes = shapes, Bounds = bounds };
        }

        /// <summary>
        /// Walks the scene graph, folding each node's transform into the points it produces.
        /// </summary>
        /// <remarks>
        /// Baking transforms rather than keeping the hierarchy: an SVG's grouping is the drawing
        /// tool's business, and carrying it into NexUI would mean a second parent/child system
        /// alongside the element hierarchy the Designer already has.
        /// </remarks>
        private static void Collect(SceneNode node, Matrix2D parent, List<NexVectorShape> output)
        {
            if (node == null) return;

            var transform = parent * node.Transform;

            if (node.Shapes != null)
            {
                for (var i = 0; i < node.Shapes.Count; i++)
                {
                    var converted = Convert(node.Shapes[i], transform);
                    if (converted != null) output.Add(converted);
                }
            }

            if (node.Children == null) return;
            for (var i = 0; i < node.Children.Count; i++) Collect(node.Children[i], transform, output);
        }

        private static NexVectorShape Convert(Shape source, Matrix2D transform)
        {
            if (source?.Contours == null || source.Contours.Length == 0) return null;

            var shape = new NexVectorShape { Filled = false, StrokeWidth = 0f };

            if (source.Fill is SolidFill solid)
            {
                shape.Filled = true;
                shape.FillColor = solid.Color;
                shape.FillRule = solid.Mode == FillMode.OddEven ? NexFillRule.OddEven : NexFillRule.NonZero;
            }
            else if (source.Fill != null)
            {
                // A gradient or pattern. The geometry is still worth importing - dropping the shape
                // would lose the artwork entirely - so it comes in as a flat fill the author can
                // then set, rather than as nothing.
                shape.Filled = true;
                shape.FillColor = Color.white;
            }

            var stroke = source.PathProps.Stroke;
            if (stroke != null && stroke.HalfThickness > 0f)
            {
                shape.StrokeWidth = stroke.HalfThickness * 2f;
                shape.StrokeColor = stroke.Color;
                shape.Join = FromCorner(source.PathProps.Corners);
                shape.Cap = FromEnding(source.PathProps.Head);
            }

            for (var i = 0; i < source.Contours.Length; i++)
            {
                var contour = FromBezierContour(source.Contours[i], transform);
                if (contour != null) shape.Contours.Add(contour);
            }

            return shape.Contours.Count == 0 ? null : shape;
        }

        /// <summary>
        /// Converts Unity's segments back to anchors with relative handles.
        /// </summary>
        /// <remarks>
        /// The inverse of what the tessellator does. A segment's <c>P1</c> is the outgoing control
        /// of its own start point and <c>P2</c> is the incoming control of the *next* point, so the
        /// two handles of one anchor come from two different segments - which is why this cannot be
        /// a straight per-segment map.
        ///
        /// A closed contour repeats its first point as the last segment's start; that repeat is
        /// dropped so the anchor list has no duplicate for the pen tool to snag on.
        /// </remarks>
        private static NexVectorContour FromBezierContour(BezierContour source, Matrix2D transform)
        {
            var segments = source.Segments;
            if (segments == null || segments.Length < 2) return null;

            // Whether the closing point is repeated is decided by the SVG, not by the Closed flag:
            // a path written "... L 0 100 L 0 0" is geometrically closed while reporting Closed
            // false, and one written with Z may or may not repeat the point. Comparing the ends is
            // the only thing that holds for both, and a duplicated first anchor is something the
            // pen tool would snag on for the rest of the shape's life.
            var closed = source.Closed;
            var count = segments.Length;

            if (count > 2 && Approximately(segments[0].P0, segments[count - 1].P0))
            {
                count--;
                closed = true;
            }

            if (count < 2) return null;

            var contour = new NexVectorContour { Closed = closed };

            for (var i = 0; i < count; i++)
            {
                var position = transform.MultiplyPoint(segments[i].P0);

                // Incoming handle lives on the previous segment; for an open contour the first
                // point simply has none.
                var previous = i > 0 ? i - 1 : (source.Closed ? count - 1 : -1);
                var inHandle = previous >= 0
                    ? transform.MultiplyPoint(segments[previous].P2) - position
                    : UnityEngine.Vector2.zero;

                var outHandle = transform.MultiplyPoint(segments[i].P1) - position;

                contour.Anchors.Add(new NexVectorAnchor(position, inHandle, outHandle));
            }

            return contour;
        }

        /// <summary>
        /// Whether two imported points are the same point.
        /// </summary>
        /// <remarks>
        /// A generous tolerance on purpose. SVG coordinates arrive as decimal text and go through
        /// a transform, so a closing point that was written identically can differ in the last
        /// bits. Being slightly too eager here merges a duplicate; being too strict leaves one,
        /// and the duplicate is the failure that reaches the user.
        /// </remarks>
        private static bool Approximately(UnityEngine.Vector2 a, UnityEngine.Vector2 b)
            => (a - b).sqrMagnitude < 1e-6f;

        private static NexLineJoin FromCorner(PathCorner corner)
        {
            switch (corner)
            {
                case PathCorner.Round: return NexLineJoin.Round;
                case PathCorner.Beveled: return NexLineJoin.Bevel;
                default: return NexLineJoin.Miter;
            }
        }

        private static NexLineCap FromEnding(PathEnding ending)
        {
            switch (ending)
            {
                case PathEnding.Round: return NexLineCap.Round;
                case PathEnding.Square: return NexLineCap.Square;
                default: return NexLineCap.Butt;
            }
        }
#endif

        private static NexSvgImportResult Failed(string error)
            => new NexSvgImportResult { Shapes = new List<NexVectorShape>(), Error = error };
    }
}
