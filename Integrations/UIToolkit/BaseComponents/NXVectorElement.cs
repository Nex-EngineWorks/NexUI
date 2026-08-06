using emiteat.NexUI.Vector;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Draws a <see cref="NexVectorShape"/> in UI Toolkit.
    /// </summary>
    /// <remarks>
    /// Painted with <see cref="Painter2D"/> rather than by uploading the tessellator's triangles.
    /// Painter2D speaks beziers, fill rules, joins and caps natively, so the path goes to the
    /// renderer as a path instead of being flattened first - which means this element does not
    /// depend on <c>Unity.VectorGraphics</c> at all. That matters: the module is Unity 6 only, so
    /// the UI Toolkit backend draws vector shapes on 2022.3 even where the uGUI one cannot.
    ///
    /// The path travels through UXML as SVG path data (see <see cref="NexVectorPathText"/>), which
    /// is what lets a generated <c>.uxml</c> carry a shape at all - an attribute is text, and a
    /// path is not. Fill and stroke ride alongside as ordinary attributes.
    ///
    /// Y axis matches the shape model: paths are stored y-down and UI Toolkit's coordinates are
    /// y-down, so nothing is mirrored here. The uGUI renderer is the one that has to flip, because
    /// uGUI's local space is y-up.
    /// </remarks>
#if UNITY_2023_2_OR_NEWER
    [UxmlElement]
#endif
    public partial class NXVectorElement : VisualElement
    {
        private NexVectorShape _shape;

        public NXVectorElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        /// <summary>
        /// The path as SVG path data - the form the shape takes in UXML.
        /// </summary>
        /// <remarks>
        /// Reading re-encodes rather than returning what was set, so the property always describes
        /// the shape that is actually being drawn even after the path was edited in place.
        /// </remarks>
#if UNITY_2023_2_OR_NEWER
        [UxmlAttribute]
#endif
        public string pathData
        {
            get => NexVectorPathText.Encode(_shape);
            set
            {
                var decoded = NexVectorPathText.Decode(value);

                // Appearance is carried by the other attributes, and decoding produces a fresh
                // shape with defaults - so whatever fill and stroke were already set are kept
                // rather than reset by assigning the path.
                if (_shape != null)
                {
                    decoded.Filled = _shape.Filled;
                    decoded.FillColor = _shape.FillColor;
                    decoded.FillRule = _shape.FillRule;
                    decoded.StrokeWidth = _shape.StrokeWidth;
                    decoded.StrokeColor = _shape.StrokeColor;
                    decoded.Join = _shape.Join;
                    decoded.Cap = _shape.Cap;
                }

                _shape = decoded;
                MarkDirtyRepaint();
            }
        }

#if UNITY_2023_2_OR_NEWER
        [UxmlAttribute]
#endif
        public Color fillColor
        {
            get => Ensure().FillColor;
            set { Ensure().FillColor = value; MarkDirtyRepaint(); }
        }

#if UNITY_2023_2_OR_NEWER
        [UxmlAttribute]
#endif
        public bool filled
        {
            get => Ensure().Filled;
            set { Ensure().Filled = value; MarkDirtyRepaint(); }
        }

#if UNITY_2023_2_OR_NEWER
        [UxmlAttribute]
#endif
        public float strokeWidth
        {
            get => Ensure().StrokeWidth;
            set { Ensure().StrokeWidth = Mathf.Max(0f, value); MarkDirtyRepaint(); }
        }

#if UNITY_2023_2_OR_NEWER
        [UxmlAttribute]
#endif
        public Color strokeColor
        {
            get => Ensure().StrokeColor;
            set { Ensure().StrokeColor = value; MarkDirtyRepaint(); }
        }

        /// <summary>
        /// The shape, created empty if the appearance attributes arrive before the path.
        /// </summary>
        /// <remarks>
        /// UXML attribute order is not guaranteed, so fill or stroke can be applied to an element
        /// that has no path yet. Without this they would be written onto a null and silently lost.
        /// </remarks>
        private NexVectorShape Ensure() => _shape ??= new NexVectorShape();

        /// <summary>
        /// The path this draws. Assigning repaints; mutating in place needs <see cref="Refresh"/>.
        /// </summary>
        public NexVectorShape Shape
        {
            get => _shape;
            set
            {
                _shape = value;
                Refresh();
            }
        }

        /// <summary>Repaints after the shape was changed in place - what a pen tool does per drag.</summary>
        public void Refresh() => MarkDirtyRepaint();

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (_shape == null || _shape.IsEmpty) return;

            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            // The path is authored in its own box and fitted to whatever the layout gave this
            // element, exactly as the uGUI renderer does, so one shape renders identically on
            // both backends.
            var bounds = _shape.Bounds();
            var scaleX = bounds.width > 0f ? rect.width / bounds.width : 1f;
            var scaleY = bounds.height > 0f ? rect.height / bounds.height : 1f;

            Vector2 Map(Vector2 point) => new Vector2(
                rect.xMin + (point.x - bounds.xMin) * scaleX,
                rect.yMin + (point.y - bounds.yMin) * scaleY);

            var painter = context.painter2D;
            painter.BeginPath();

            var drew = false;
            for (var c = 0; c < _shape.Contours.Count; c++)
            {
                var contour = _shape.Contours[c];
                if (contour == null || contour.Anchors.Count < 2) continue;

                var anchors = contour.Anchors;
                painter.MoveTo(Map(anchors[0].Position));

                var segments = contour.Closed ? anchors.Count : anchors.Count - 1;
                for (var i = 0; i < segments; i++)
                {
                    var from = anchors[i];
                    var to = anchors[(i + 1) % anchors.Count];

                    painter.BezierCurveTo(
                        Map(from.Position + from.OutHandle),
                        Map(to.Position + to.InHandle),
                        Map(to.Position));
                }

                // Closing the sub-path is what lets several contours act as one compound shape,
                // which is how a hole is expressed - the fill rule then decides what is inside.
                if (contour.Closed) painter.ClosePath();
                drew = true;
            }

            if (!drew) return;

            if (_shape.Filled)
            {
                painter.fillColor = _shape.FillColor;
                painter.Fill(_shape.FillRule == NexFillRule.OddEven ? FillRule.OddEven : FillRule.NonZero);
            }

            if (!_shape.HasStroke) return;

            painter.strokeColor = _shape.StrokeColor;

            // Stroke width follows the fit, so a shape scaled into a larger element keeps its
            // outline in proportion instead of growing a hairline.
            painter.lineWidth = _shape.StrokeWidth * Mathf.Min(Mathf.Abs(scaleX), Mathf.Abs(scaleY));
            painter.lineJoin = ToLineJoin(_shape.Join);
            painter.lineCap = ToLineCap(_shape.Cap);
            painter.Stroke();
        }

        private static LineJoin ToLineJoin(NexLineJoin join)
        {
            switch (join)
            {
                case NexLineJoin.Round: return LineJoin.Round;
                case NexLineJoin.Bevel: return LineJoin.Bevel;
                default: return LineJoin.Miter;
            }
        }

        private static LineCap ToLineCap(NexLineCap cap)
        {
            switch (cap)
            {
                case NexLineCap.Round: return LineCap.Round;

                // UI Toolkit has no distinct square cap; Butt is the closer of the two it offers,
                // since Round would visibly change a shape authored with square ends.
                default: return LineCap.Butt;
            }
        }
    }
}
