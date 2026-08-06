using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Vector
{
    /// <summary>Which part of an anchor a hit test landed on.</summary>
    public enum NexAnchorPart
    {
        None = 0,
        Point = 1,
        InHandle = 2,
        OutHandle = 3
    }

    /// <summary>A located piece of a path: which contour, which anchor, and which part of it.</summary>
    public readonly struct NexPathHit
    {
        public readonly int Contour;
        public readonly int Anchor;
        public readonly NexAnchorPart Part;

        public NexPathHit(int contour, int anchor, NexAnchorPart part)
        {
            Contour = contour;
            Anchor = anchor;
            Part = part;
        }

        public static readonly NexPathHit None = new NexPathHit(-1, -1, NexAnchorPart.None);

        public bool Found => Part != NexAnchorPart.None;
    }

    /// <summary>
    /// The editing operations a pen tool performs, separated from the UI that triggers them.
    /// </summary>
    /// <remarks>
    /// Kept out of the viewport on purpose. Anchor insertion, handle mirroring and curve splitting
    /// are where a pen tool is actually wrong or right, and they are exactly the parts that cannot
    /// be tested through an EditorWindow. The viewport is then only hit-testing and dragging.
    ///
    /// Every operation mutates the shape in place and reports whether anything changed, so the
    /// caller knows when to push an undo entry and when a drag produced nothing.
    /// </remarks>
    public static class NexPathEditing
    {
        /// <summary>Finds the anchor or handle under a point, preferring handles.</summary>
        /// <remarks>
        /// Handles win ties because they sit on top of their anchor when the curvature is small,
        /// and a user aiming at a visible handle would otherwise grab the point underneath it and
        /// move the whole anchor.
        /// </remarks>
        public static NexPathHit HitTest(NexVectorShape shape, Vector2 point, float radius)
        {
            if (shape == null) return NexPathHit.None;

            var squared = radius * radius;
            var best = NexPathHit.None;
            var bestDistance = float.MaxValue;

            for (var c = 0; c < shape.Contours.Count; c++)
            {
                var anchors = shape.Contours[c]?.Anchors;
                if (anchors == null) continue;

                for (var a = 0; a < anchors.Count; a++)
                {
                    var anchor = anchors[a];

                    Consider(anchor.Position + anchor.InHandle, NexAnchorPart.InHandle);
                    Consider(anchor.Position + anchor.OutHandle, NexAnchorPart.OutHandle);
                    Consider(anchor.Position, NexAnchorPart.Point);

                    void Consider(Vector2 candidate, NexAnchorPart part)
                    {
                        // A zero handle is not drawn, so it must not be grabbable either.
                        if (part != NexAnchorPart.Point && candidate == anchor.Position) return;

                        var distance = (candidate - point).sqrMagnitude;
                        if (distance > squared || distance >= bestDistance) return;

                        bestDistance = distance;
                        best = new NexPathHit(c, a, part);
                    }
                }
            }

            return best;
        }

        /// <summary>Moves whatever the hit refers to, keeping handles attached to their anchor.</summary>
        public static bool Move(NexVectorShape shape, NexPathHit hit, Vector2 delta, bool mirrorHandles = true)
        {
            if (!hit.Found || delta == Vector2.zero) return false;
            var anchors = AnchorsOf(shape, hit.Contour);
            if (anchors == null || hit.Anchor >= anchors.Count) return false;

            var anchor = anchors[hit.Anchor];

            switch (hit.Part)
            {
                case NexAnchorPart.Point:
                    // Handles are stored relative to the point, so moving the point carries the
                    // curvature with it and there is nothing else to update.
                    anchor.Position += delta;
                    break;

                case NexAnchorPart.InHandle:
                    anchor.InHandle += delta;
                    if (mirrorHandles && anchor.OutHandle != Vector2.zero) anchor.OutHandle = -anchor.InHandle;
                    break;

                case NexAnchorPart.OutHandle:
                    anchor.OutHandle += delta;
                    if (mirrorHandles && anchor.InHandle != Vector2.zero) anchor.InHandle = -anchor.OutHandle;
                    break;

                default:
                    return false;
            }

            anchors[hit.Anchor] = anchor;
            return true;
        }

        /// <summary>Adds an anchor at the end of a contour - the click-to-draw case.</summary>
        public static bool Append(NexVectorShape shape, int contourIndex, Vector2 position)
        {
            var anchors = AnchorsOf(shape, contourIndex);
            if (anchors == null) return false;

            anchors.Add(new NexVectorAnchor(position));
            return true;
        }

        /// <summary>
        /// Inserts an anchor on the segment after <paramref name="afterAnchor"/>, splitting it.
        /// </summary>
        /// <remarks>
        /// De Casteljau subdivision rather than dropping a point on the curve: subdividing produces
        /// two segments whose union is the original curve exactly, so the shape does not visibly
        /// twitch when a point is added. Placing a point and guessing handles does twitch, which is
        /// the single most noticeable way a pen tool feels wrong.
        /// </remarks>
        public static bool InsertOnSegment(NexVectorShape shape, int contourIndex, int afterAnchor, float t)
        {
            var contour = ContourOf(shape, contourIndex);
            var anchors = contour?.Anchors;
            if (anchors == null || afterAnchor < 0 || afterAnchor >= anchors.Count) return false;

            var isLast = afterAnchor == anchors.Count - 1;
            if (isLast && !contour.Closed) return false;

            var nextIndex = (afterAnchor + 1) % anchors.Count;
            var start = anchors[afterAnchor];
            var end = anchors[nextIndex];

            t = Mathf.Clamp01(t);

            var p0 = start.Position;
            var p1 = start.Position + start.OutHandle;
            var p2 = end.Position + end.InHandle;
            var p3 = end.Position;

            var a = Vector2.Lerp(p0, p1, t);
            var b = Vector2.Lerp(p1, p2, t);
            var c = Vector2.Lerp(p2, p3, t);
            var d = Vector2.Lerp(a, b, t);
            var e = Vector2.Lerp(b, c, t);
            var split = Vector2.Lerp(d, e, t);

            start.OutHandle = a - p0;
            end.InHandle = c - p3;

            anchors[afterAnchor] = start;
            anchors[nextIndex] = end;
            anchors.Insert(afterAnchor + 1, new NexVectorAnchor(split, d - split, e - split));
            return true;
        }

        /// <summary>Removes an anchor, refusing to leave a contour too small to be a shape.</summary>
        public static bool RemoveAnchor(NexVectorShape shape, int contourIndex, int anchorIndex)
        {
            var anchors = AnchorsOf(shape, contourIndex);
            if (anchors == null || anchorIndex < 0 || anchorIndex >= anchors.Count) return false;

            // Below two anchors there is no path left, only a point that draws nothing and cannot
            // be got out of. Deleting the contour would be a bigger surprise than refusing.
            if (anchors.Count <= 2) return false;

            anchors.RemoveAt(anchorIndex);
            return true;
        }

        /// <summary>Turns a curve point into a corner, or restores a curve through it.</summary>
        /// <remarks>
        /// The restored handles point along the neighbouring anchors at a third of the distance -
        /// the shape a drawing tool produces when smoothing a corner, and close enough to a
        /// circular arc that it reads as intentional rather than arbitrary.
        /// </remarks>
        public static bool ToggleCorner(NexVectorShape shape, int contourIndex, int anchorIndex)
        {
            var contour = ContourOf(shape, contourIndex);
            var anchors = contour?.Anchors;
            if (anchors == null || anchorIndex < 0 || anchorIndex >= anchors.Count) return false;

            var anchor = anchors[anchorIndex];

            if (!anchor.IsCorner)
            {
                anchor.InHandle = Vector2.zero;
                anchor.OutHandle = Vector2.zero;
                anchors[anchorIndex] = anchor;
                return true;
            }

            if (anchors.Count < 3) return false;

            var previous = anchors[(anchorIndex - 1 + anchors.Count) % anchors.Count].Position;
            var next = anchors[(anchorIndex + 1) % anchors.Count].Position;
            var direction = (next - previous) * (1f / 3f);

            anchor.InHandle = -direction;
            anchor.OutHandle = direction;
            anchors[anchorIndex] = anchor;
            return true;
        }

        private static NexVectorContour ContourOf(NexVectorShape shape, int index)
            => shape != null && index >= 0 && index < shape.Contours.Count ? shape.Contours[index] : null;

        private static List<NexVectorAnchor> AnchorsOf(NexVectorShape shape, int index)
            => ContourOf(shape, index)?.Anchors;
    }
}
