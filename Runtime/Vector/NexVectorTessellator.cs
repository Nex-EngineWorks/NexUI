using System.Collections.Generic;
using UnityEngine;
#if NEXUI_VECTOR_GRAPHICS
using Unity.VectorGraphics;
#endif

namespace emiteat.NexUI.Vector
{
    /// <summary>
    /// How finely curves are flattened into line segments.
    /// </summary>
    /// <remarks>
    /// NexUI's own type rather than the tessellator's, so callers - and the uGUI renderer in
    /// particular - do not have to be conditionally compiled alongside it.
    ///
    /// The defaults are tuned for screen-space UI, where a shape is tens to hundreds of pixels
    /// across. Unity's own defaults target sprites that may be scaled up arbitrarily, and they
    /// produce far more triangles than a button needs.
    /// </remarks>
    public struct NexTessellationOptions
    {
        public float StepDistance;
        public float MaxCordDeviation;
        public float MaxTanAngleDeviation;
        public float SamplingStepSize;
    }

    /// <summary>
    /// Turns a <see cref="NexVectorShape"/> into triangles, using Unity's own vector tessellator.
    /// </summary>
    /// <remarks>
    /// The tessellator carries the parts that are genuinely hard: bezier flattening, fill-rule
    /// handling for self-overlapping paths, and stroke geometry with joins and caps. Writing those
    /// again would be a lot of subtle code to arrive at the same place, and v3-final §2.5 rules out
    /// building a rendering stack of our own regardless.
    ///
    /// What stays on this side is the translation: NexUI's anchors carry handles relative to the
    /// point while Unity's segments carry absolute control points. Keeping that conversion in one
    /// place is what stops the difference from leaking into the editor and the renderer separately.
    ///
    /// <para><b>Availability.</b> <c>Unity.VectorGraphics</c> is a built-in engine module on Unity 6
    /// but does not exist on 2022.3, where it is the optional <c>com.unity.vectorgraphics</c>
    /// package. Both define <c>NEXUI_VECTOR_GRAPHICS</c> through this assembly's version defines.
    /// Without either, the path model, the pen tool and the compile path all still work - only
    /// triangulation is unavailable, and <see cref="IsSupported"/> says so rather than the project
    /// failing to compile.</para>
    /// </remarks>
    public static class NexVectorTessellator
    {
        /// <summary>Triangulated output, in the same space the shape was authored in.</summary>
        public struct Mesh2D
        {
            public Vector2[] Vertices;
            public ushort[] Indices;
            public Color Color;

            public bool IsEmpty => Vertices == null || Vertices.Length == 0
                                   || Indices == null || Indices.Length < 3;
        }

        /// <summary>
        /// Whether this build can actually produce geometry.
        /// </summary>
        /// <remarks>
        /// Public so a caller can say why a shape is not drawing. A silently empty mesh is
        /// indistinguishable from a mis-authored path, and that is the wrong thing to make somebody
        /// debug.
        /// </remarks>
        public static bool IsSupported =>
#if NEXUI_VECTOR_GRAPHICS
            true;
#else
            false;
#endif

        /// <summary>Explains an unsupported build, or empty when tessellation is available.</summary>
        public static string UnsupportedReason => IsSupported
            ? string.Empty
            : "Vector shapes need Unity's vector graphics module. It is built in on Unity 6; " +
              "on Unity 2022.3 install the com.unity.vectorgraphics package.";

        public static NexTessellationOptions DefaultOptions => new NexTessellationOptions
        {
            StepDistance = 100f,
            MaxCordDeviation = 0.25f,
            MaxTanAngleDeviation = 0.05f,
            SamplingStepSize = 0.01f
        };

        /// <summary>Tessellates fill and stroke. Returns an empty list for an empty shape.</summary>
        public static List<Mesh2D> Tessellate(NexVectorShape shape)
            => Tessellate(shape, DefaultOptions);

        public static List<Mesh2D> Tessellate(NexVectorShape shape, NexTessellationOptions options)
        {
            var result = new List<Mesh2D>();
            if (shape == null || shape.IsEmpty) return result;

#if NEXUI_VECTOR_GRAPHICS
            var scene = new Scene { Root = new SceneNode { Shapes = new List<Shape>() } };

            var contours = new List<BezierContour>(shape.Contours.Count);
            for (var i = 0; i < shape.Contours.Count; i++)
            {
                var contour = ToBezierContour(shape.Contours[i]);
                if (contour.Segments != null && contour.Segments.Length >= 2) contours.Add(contour);
            }

            if (contours.Count == 0) return result;

            var drawn = new Shape
            {
                Contours = contours.ToArray(),
                Fill = shape.Filled
                    ? new SolidFill { Color = shape.FillColor, Mode = ToFillMode(shape.FillRule) }
                    : null
            };

            if (shape.HasStroke)
            {
                drawn.PathProps = new PathProperties
                {
                    Stroke = new Stroke { Color = shape.StrokeColor, HalfThickness = shape.StrokeWidth * 0.5f },
                    Corners = ToCorner(shape.Join),
                    Head = ToEnding(shape.Cap),
                    Tail = ToEnding(shape.Cap)
                };
            }

            scene.Root.Shapes.Add(drawn);

            var geometry = VectorUtils.TessellateScene(scene, ToTessellationOptions(options));
            for (var i = 0; i < geometry.Count; i++)
            {
                var piece = geometry[i];
                if (piece.Vertices == null || piece.Indices == null) continue;

                result.Add(new Mesh2D
                {
                    Vertices = piece.Vertices,
                    Indices = piece.Indices,
                    Color = piece.Color
                });
            }
#endif

            return result;
        }

#if NEXUI_VECTOR_GRAPHICS
        private static VectorUtils.TessellationOptions ToTessellationOptions(NexTessellationOptions options)
            => new VectorUtils.TessellationOptions
            {
                StepDistance = options.StepDistance,
                MaxCordDeviation = options.MaxCordDeviation,
                MaxTanAngleDeviation = options.MaxTanAngleDeviation,
                SamplingStepSize = options.SamplingStepSize
            };

        /// <summary>
        /// Converts a contour to Unity's segment form.
        /// </summary>
        /// <remarks>
        /// Unity stores each segment as a start point plus two absolute control points, with the
        /// next segment's start acting as the end. NexUI stores handles relative to their anchor,
        /// which is what a pen tool needs. The conversion is per-segment rather than per-point:
        /// a segment's controls come from the *outgoing* handle of one anchor and the *incoming*
        /// handle of the next.
        /// </remarks>
        private static BezierContour ToBezierContour(NexVectorContour contour)
        {
            if (contour == null || contour.Anchors.Count < 2) return default;

            var anchors = contour.Anchors;
            var count = anchors.Count;

            // A closed contour needs a trailing segment back to the start; Unity expresses that by
            // repeating the first point as the final segment's start.
            var segmentCount = contour.Closed ? count + 1 : count;
            var segments = new BezierPathSegment[segmentCount];

            for (var i = 0; i < segmentCount; i++)
            {
                var current = anchors[i % count];
                var next = anchors[(i + 1) % count];

                segments[i] = new BezierPathSegment
                {
                    P0 = current.Position,
                    P1 = current.Position + current.OutHandle,
                    P2 = next.Position + next.InHandle
                };
            }

            return new BezierContour { Segments = segments, Closed = contour.Closed };
        }

        private static FillMode ToFillMode(NexFillRule rule)
            => rule == NexFillRule.OddEven ? FillMode.OddEven : FillMode.NonZero;

        private static PathCorner ToCorner(NexLineJoin join)
        {
            switch (join)
            {
                case NexLineJoin.Round: return PathCorner.Round;
                case NexLineJoin.Bevel: return PathCorner.Beveled;
                default: return PathCorner.Tipped;
            }
        }

        private static PathEnding ToEnding(NexLineCap cap)
        {
            switch (cap)
            {
                case NexLineCap.Round: return PathEnding.Round;
                case NexLineCap.Square: return PathEnding.Square;
                default: return PathEnding.Chop;
            }
        }
#endif
    }
}
