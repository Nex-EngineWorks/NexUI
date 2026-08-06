using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Vector
{
    /// <summary>
    /// One point on a path, with the handles that shape the curve through it.
    /// </summary>
    /// <remarks>
    /// Handles are stored relative to the point, which is what makes moving a point drag its
    /// curvature along instead of leaving the handles behind - the behaviour every pen tool has.
    ///
    /// A handle at zero means "straight into this point". That is how a polygon is expressed
    /// without a second representation: the same anchor list draws a star or a blob depending on
    /// whether the handles are zero, so the editor never has to convert between "corner mode" and
    /// "curve mode".
    /// </remarks>
    [Serializable]
    public struct NexVectorAnchor
    {
        public Vector2 Position;

        /// <summary>Handle controlling the curve arriving at this point, relative to it.</summary>
        public Vector2 InHandle;

        /// <summary>Handle controlling the curve leaving this point, relative to it.</summary>
        public Vector2 OutHandle;

        public NexVectorAnchor(Vector2 position, Vector2 inHandle = default, Vector2 outHandle = default)
        {
            Position = position;
            InHandle = inHandle;
            OutHandle = outHandle;
        }

        /// <summary>Whether this point is a hard corner rather than a smooth curve.</summary>
        public bool IsCorner => InHandle == Vector2.zero && OutHandle == Vector2.zero;
    }

    /// <summary>How the inside of a self-overlapping path is decided.</summary>
    public enum NexFillRule
    {
        /// <summary>Overlaps stay filled. The default, and what most drawing produces.</summary>
        NonZero = 0,

        /// <summary>Overlaps cut holes. What makes a donut from two circles in one path.</summary>
        OddEven = 1
    }

    public enum NexLineJoin { Miter = 0, Round = 1, Bevel = 2 }

    public enum NexLineCap { Butt = 0, Round = 1, Square = 2 }

    /// <summary>
    /// A closed or open run of anchors. Several contours in one shape make a compound path.
    /// </summary>
    /// <remarks>
    /// A hole is a second contour, not a boolean operation: that is how SVG and every vector editor
    /// express the letter "O", and it costs nothing but a fill rule. True boolean operations
    /// (union, subtract) are a separate feature that produces new contours - this is the
    /// representation they would produce into.
    /// </remarks>
    [Serializable]
    public sealed class NexVectorContour
    {
        [SerializeField] private List<NexVectorAnchor> _anchors = new List<NexVectorAnchor>();

        [SerializeField] private bool _closed = true;

        public List<NexVectorAnchor> Anchors => _anchors;

        public bool Closed
        {
            get => _closed;
            set => _closed = value;
        }

        public NexVectorContour() { }

        public NexVectorContour(IEnumerable<NexVectorAnchor> anchors, bool closed = true)
        {
            if (anchors != null) _anchors.AddRange(anchors);
            _closed = closed;
        }

        /// <summary>Axis-aligned bounds of the anchor points, ignoring handle overshoot.</summary>
        /// <remarks>
        /// Anchor bounds rather than true curve bounds. Exact bounds need the curve extrema, and
        /// every caller here uses this to fit a shape into a rect - where the anchors are what the
        /// author positioned and what they expect to line up.
        /// </remarks>
        public Rect Bounds()
        {
            if (_anchors.Count == 0) return Rect.zero;

            var min = _anchors[0].Position;
            var max = min;

            for (var i = 1; i < _anchors.Count; i++)
            {
                var p = _anchors[i].Position;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return new Rect(min, max - min);
        }
    }

    /// <summary>
    /// A vector shape: its contours, how it is filled, and how it is stroked.
    /// </summary>
    /// <remarks>
    /// Deliberately backend-neutral and serializable by Unity, so the same object is what the
    /// Designer edits, what the compiled program carries, and what the uGUI renderer tessellates.
    /// Tessellation itself is not here - it belongs to whatever draws the shape, and keeping the
    /// model free of it is what lets a UI Toolkit renderer consume the same data later.
    /// </remarks>
    [Serializable]
    public sealed class NexVectorShape
    {
        [SerializeField] private List<NexVectorContour> _contours = new List<NexVectorContour>();

        public List<NexVectorContour> Contours => _contours;

        public NexFillRule FillRule = NexFillRule.NonZero;

        public bool Filled = true;

        public Color FillColor = Color.white;

        /// <summary>Stroke width in pixels. Zero draws no outline.</summary>
        public float StrokeWidth;

        public Color StrokeColor = Color.black;

        public NexLineJoin Join = NexLineJoin.Miter;

        public NexLineCap Cap = NexLineCap.Butt;

        public bool HasStroke => StrokeWidth > 0f && StrokeColor.a > 0f;

        public bool IsEmpty
        {
            get
            {
                for (var i = 0; i < _contours.Count; i++)
                    if (_contours[i] != null && _contours[i].Anchors.Count >= 2) return false;
                return true;
            }
        }

        public Rect Bounds()
        {
            var bounds = Rect.zero;
            var started = false;

            for (var i = 0; i < _contours.Count; i++)
            {
                var contour = _contours[i];
                if (contour == null || contour.Anchors.Count == 0) continue;

                var next = contour.Bounds();
                if (!started) { bounds = next; started = true; continue; }

                var min = Vector2.Min(bounds.min, next.min);
                var max = Vector2.Max(bounds.max, next.max);
                bounds = new Rect(min, max - min);
            }

            return bounds;
        }

        public NexVectorShape Clone()
        {
            var clone = new NexVectorShape
            {
                FillRule = FillRule,
                Filled = Filled,
                FillColor = FillColor,
                StrokeWidth = StrokeWidth,
                StrokeColor = StrokeColor,
                Join = Join,
                Cap = Cap
            };

            for (var i = 0; i < _contours.Count; i++)
            {
                var contour = _contours[i];
                if (contour == null) continue;
                clone._contours.Add(new NexVectorContour(contour.Anchors, contour.Closed));
            }

            return clone;
        }
    }
}
