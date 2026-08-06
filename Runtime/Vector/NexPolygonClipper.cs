using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Vector
{
    /// <summary>Which parts of two shapes a boolean operation keeps.</summary>
    public enum NexBooleanOperation
    {
        /// <summary>Everything covered by either shape.</summary>
        Union = 0,

        /// <summary>Only what both shapes cover.</summary>
        Intersect = 1,

        /// <summary>The first shape with the second cut out of it.</summary>
        Subtract = 2,

        /// <summary>Everything covered by exactly one of the two - the overlap becomes a hole.</summary>
        Exclude = 3
    }

    /// <summary>
    /// Boolean operations on polygons, by the Martínez-Rueda sweep-line algorithm.
    /// </summary>
    /// <remarks>
    /// A sweep line rather than the shorter Greiner-Hormann because of degeneracies. Shapes drawn
    /// in a UI are full of them - a badge snapped flush against a card edge, two rectangles sharing
    /// a corner, a shape subtracted from a copy of itself - and Greiner-Hormann is undefined
    /// whenever a vertex lands exactly on the other polygon's edge. That is not an exotic input
    /// here, it is what snapping produces, so the algorithm has to handle it rather than be
    /// perturbed around it.
    ///
    /// All four operations are the same sweep; they differ only in
    /// <see cref="ShouldKeep"/> - which edges of the arrangement end up in the result.
    ///
    /// <para><b>Scale.</b> Everything is done in the caller's coordinates with a fixed epsilon.
    /// That is sound for UI shapes, which live in pixels over a range of a few thousand; it would
    /// not be for coordinates spanning many orders of magnitude.</para>
    /// </remarks>
    public static class NexPolygonClipper
    {
        /// <summary>
        /// Below this, two coordinates are the same point.
        /// </summary>
        /// <remarks>
        /// Sized for pixels: far below anything a person can position, far above the noise of
        /// accumulating float error through an intersection calculation.
        /// </remarks>
        private const float Epsilon = 1e-5f;

        private enum PolygonSide { Subject = 0, Clip = 1 }

        private enum EdgeKind
        {
            Normal,

            /// <summary>An edge duplicated by the other polygon; only one copy may contribute.</summary>
            NonContributing,

            /// <summary>Overlapping edges whose polygons transition the same way.</summary>
            SameTransition,

            /// <summary>Overlapping edges whose polygons transition oppositely.</summary>
            DifferentTransition
        }

        private sealed class SweepEvent
        {
            public Vector2 Point;
            public bool Left;
            public SweepEvent Other;
            public PolygonSide Side;
            public EdgeKind Kind = EdgeKind.Normal;

            /// <summary>Whether this edge is an inside-to-outside transition of its own polygon.</summary>
            public bool InOut;

            /// <summary>The same, for the nearest edge below belonging to the other polygon.</summary>
            public bool OtherInOut;

            /// <summary>Nearest edge below that is itself in the result - the parent for holes.</summary>
            public SweepEvent PrevInResult;

            public bool InResult;
            public int Position;
            public bool ResultInOut;
            public int ContourId = -1;

            /// <summary>Insertion order, used only to break ties that are otherwise undecidable.</summary>
            public int Serial;

            /// <summary>Whether <paramref name="point"/> lies above this event's segment.</summary>
            public bool Above(Vector2 point) => !Below(point);

            public bool Below(Vector2 point) => Left
                ? SignedArea(Point, Other.Point, point) > 0f
                : SignedArea(Other.Point, Point, point) > 0f;

            public bool Vertical => Mathf.Abs(Point.x - Other.Point.x) < Epsilon;
        }

        /// <summary>
        /// Combines two sets of closed rings.
        /// </summary>
        /// <param name="subject">The first operand. For <see cref="NexBooleanOperation.Subtract"/> this is what is kept.</param>
        /// <param name="clip">The second operand.</param>
        /// <param name="operation">Which parts to keep.</param>
        /// <returns>
        /// New rings. Outer rings wind one way and holes the other, so the result draws correctly
        /// under the non-zero fill rule.
        /// </returns>
        public static List<List<Vector2>> Clip(
            IReadOnlyList<IReadOnlyList<Vector2>> subject,
            IReadOnlyList<IReadOnlyList<Vector2>> clip,
            NexBooleanOperation operation)
        {
            var subjectEmpty = IsEmpty(subject);
            var clipEmpty = IsEmpty(clip);

            // Trivial cases, answered before building any events. Beyond being fast, this is what
            // makes "subtract nothing" and "intersect with nothing" behave sensibly rather than
            // fall out of an algorithm that was never given an edge to sweep.
            if (subjectEmpty || clipEmpty)
            {
                switch (operation)
                {
                    case NexBooleanOperation.Intersect:
                        return new List<List<Vector2>>();
                    case NexBooleanOperation.Subtract:
                        return subjectEmpty ? new List<List<Vector2>>() : CopyRings(subject);
                    default:
                        return subjectEmpty ? CopyRings(clip) : CopyRings(subject);
                }
            }

            var queue = new EventQueue();
            var serial = 0;

            AddRings(queue, subject, PolygonSide.Subject, ref serial);
            AddRings(queue, clip, PolygonSide.Clip, ref serial);

            var sorted = Sweep(queue, operation);
            return ConnectEdges(sorted);
        }

        /// <summary>
        /// Reports the arrangement the sweep produced, one line per segment. For diagnosing a
        /// boolean result that is wrong.
        /// </summary>
        /// <remarks>
        /// A wrong result says only that something is wrong. The flags here say where:
        /// <c>otherInOut</c> is what every operation's keep-or-drop decision reads, so a segment
        /// kept when it should be dropped is either a bad flag or a bad rule, and those two are
        /// indistinguishable from the output polygon alone.
        ///
        /// Not gated behind a test define: the define only exists in test assemblies, and this
        /// lives in a runtime one. It allocates and formats strings, so it is for answering a
        /// question, never for the drawing path.
        /// </remarks>
        public static List<string> Explain(
            IReadOnlyList<IReadOnlyList<Vector2>> subject,
            IReadOnlyList<IReadOnlyList<Vector2>> clip,
            NexBooleanOperation operation)
        {
            var queue = new EventQueue();
            var serial = 0;

            AddRings(queue, subject, PolygonSide.Subject, ref serial);
            AddRings(queue, clip, PolygonSide.Clip, ref serial);

            var sorted = Sweep(queue, operation);
            var lines = new List<string>();

            for (var i = 0; i < sorted.Count; i++)
            {
                var e = sorted[i];
                if (!e.Left) continue;

                lines.Add(
                    $"({e.Point.x:0.#},{e.Point.y:0.#})->({e.Other.Point.x:0.#},{e.Other.Point.y:0.#}) " +
                    $"{e.Side} inOut={e.InOut} otherInOut={e.OtherInOut} " +
                    $"kind={e.Kind} keep={e.InResult}");
            }

            return lines;
        }

        // ---- setup -----------------------------------------------------------

        private static bool IsEmpty(IReadOnlyList<IReadOnlyList<Vector2>> rings)
        {
            if (rings == null) return true;
            for (var i = 0; i < rings.Count; i++)
                if (rings[i] != null && rings[i].Count >= 3) return false;
            return true;
        }

        private static List<List<Vector2>> CopyRings(IReadOnlyList<IReadOnlyList<Vector2>> rings)
        {
            var result = new List<List<Vector2>>();
            if (rings == null) return result;

            for (var i = 0; i < rings.Count; i++)
            {
                if (rings[i] == null || rings[i].Count < 3) continue;
                result.Add(new List<Vector2>(rings[i]));
            }

            return result;
        }

        private static void AddRings(EventQueue queue, IReadOnlyList<IReadOnlyList<Vector2>> rings,
            PolygonSide side, ref int serial)
        {
            for (var r = 0; r < rings.Count; r++)
            {
                var ring = rings[r];
                if (ring == null || ring.Count < 3) continue;

                for (var i = 0; i < ring.Count; i++)
                {
                    var from = ring[i];
                    var to = ring[(i + 1) % ring.Count];
                    if (Same(from, to)) continue;

                    var left = new SweepEvent { Point = from, Side = side, Serial = serial++ };
                    var right = new SweepEvent { Point = to, Side = side, Serial = serial++ };
                    left.Other = right;
                    right.Other = left;

                    // "Left" means the endpoint the sweep reaches first, which is what every
                    // ordering decision below is phrased in terms of.
                    if (CompareEvents(left, right) < 0)
                    {
                        left.Left = true;
                        right.Left = false;
                    }
                    else
                    {
                        left.Left = false;
                        right.Left = true;
                    }

                    queue.Push(left);
                    queue.Push(right);
                }
            }
        }

        // ---- the sweep -------------------------------------------------------

        private static List<SweepEvent> Sweep(EventQueue queue, NexBooleanOperation operation)
        {
            var status = new StatusLine();
            var output = new List<SweepEvent>();

            while (queue.Count > 0)
            {
                var current = queue.Pop();
                output.Add(current);

                if (current.Left)
                {
                    var index = status.Insert(current);
                    var below = status.Below(index);
                    var above = status.Above(index);

                    ComputeFields(current, below, operation);

                    if (above != null && PossibleIntersection(current, above, queue) == 2)
                    {
                        // The pair turned out to be collinear and overlapping, so the flags just
                        // computed were based on a picture that no longer holds.
                        ComputeFields(current, below, operation);
                        ComputeFields(above, current, operation);
                    }

                    if (below != null && PossibleIntersection(below, current, queue) == 2)
                    {
                        var belowBelow = status.Below(status.IndexOf(below));
                        ComputeFields(below, belowBelow, operation);
                        ComputeFields(current, below, operation);
                    }
                }
                else
                {
                    // A right endpoint closes its segment; the neighbours it was separating can
                    // now meet, so they have to be tested against each other.
                    var index = status.IndexOf(current.Other);
                    if (index < 0) continue;

                    var below = status.Below(index);
                    var above = status.Above(index);
                    status.RemoveAt(index);

                    if (below != null && above != null) PossibleIntersection(below, above, queue);
                }
            }

            output.Sort(CompareEvents);
            return output;
        }

        /// <summary>
        /// Works out where an edge sits relative to the other polygon, from the edge below it.
        /// </summary>
        private static void ComputeFields(SweepEvent current, SweepEvent below, NexBooleanOperation operation)
        {
            if (below == null)
            {
                // Nothing below: this edge is entering its own polygon from the outside, and the
                // other polygon is not present here at all.
                current.InOut = false;
                current.OtherInOut = true;
            }
            else if (current.Side == below.Side)
            {
                current.InOut = !below.InOut;
                current.OtherInOut = below.OtherInOut;
            }
            else
            {
                // Crossing into the other polygon's territory swaps which flag means what.
                current.InOut = !below.OtherInOut;
                current.OtherInOut = below.Vertical ? !below.InOut : below.InOut;
            }

            current.PrevInResult = below != null && (!ShouldKeep(below, operation) || below.Vertical)
                ? below.PrevInResult
                : below;

            current.InResult = ShouldKeep(current, operation);
        }

        /// <summary>Whether an edge of the arrangement belongs to this operation's boundary.</summary>
        private static bool ShouldKeep(SweepEvent e, NexBooleanOperation operation)
        {
            switch (e.Kind)
            {
                case EdgeKind.Normal:
                    switch (operation)
                    {
                        case NexBooleanOperation.Intersect: return !e.OtherInOut;
                        case NexBooleanOperation.Union: return e.OtherInOut;
                        case NexBooleanOperation.Exclude: return true;
                        case NexBooleanOperation.Subtract:
                            // Keep the subject's outside boundary and the clip's inside boundary:
                            // together they trace the subject with a bite taken out.
                            return (e.Side == PolygonSide.Subject && e.OtherInOut)
                                   || (e.Side == PolygonSide.Clip && !e.OtherInOut);
                        default: return false;
                    }

                case EdgeKind.SameTransition:
                    return operation == NexBooleanOperation.Intersect || operation == NexBooleanOperation.Union;

                case EdgeKind.DifferentTransition:
                    return operation == NexBooleanOperation.Subtract;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Splits two segments where they meet, so the arrangement has no crossings left.
        /// </summary>
        /// <returns>0 for no intersection, 1 for a point, 2 for a collinear overlap.</returns>
        private static int PossibleIntersection(SweepEvent e1, SweepEvent e2, EventQueue queue)
        {
            var count = FindIntersection(e1.Point, e1.Other.Point, e2.Point, e2.Other.Point,
                out var first, out var second);

            if (count == 0) return 0;

            // Sharing exactly one endpoint is two edges meeting at a vertex, which is already a
            // valid arrangement - splitting there would only create zero-length edges.
            if (count == 1 && (Same(e1.Point, e2.Point) || Same(e1.Other.Point, e2.Other.Point))) return 0;

            if (count == 2 && e1.Side == e2.Side)
            {
                // Two edges of the *same* polygon lying on top of each other is a self-overlap.
                // There is no meaningful in/out answer, so the input is left as it is rather than
                // producing a result that implies one.
                return 0;
            }

            if (count == 1)
            {
                if (!Same(e1.Point, first) && !Same(e1.Other.Point, first)) Divide(e1, first, queue);
                if (!Same(e2.Point, first) && !Same(e2.Other.Point, first)) Divide(e2, first, queue);
                return 1;
            }

            // Collinear overlap: cut both segments at every shared boundary so the overlapping
            // stretch becomes one edge in each polygon, then mark one of them non-contributing.
            var order = new List<SweepEvent>();
            var leftEqual = Same(e1.Point, e2.Point);
            var rightEqual = Same(e1.Other.Point, e2.Other.Point);

            if (!leftEqual) order.Add(CompareEvents(e1, e2) < 0 ? e1 : e2);
            if (!rightEqual) order.Add(CompareEvents(e1.Other, e2.Other) > 0 ? e1 : e2);

            if (leftEqual)
            {
                // Same start: the shorter one ends first, and the longer is split there.
                e1.Kind = e2.Kind = EdgeKind.NonContributing;
                var overlapping = e1.InOut == e2.InOut ? EdgeKind.SameTransition : EdgeKind.DifferentTransition;

                if (rightEqual)
                {
                    e1.Kind = EdgeKind.NonContributing;
                    e2.Kind = overlapping;
                    return 2;
                }

                var longer = CompareEvents(e1.Other, e2.Other) > 0 ? e1 : e2;
                var shorter = ReferenceEquals(longer, e1) ? e2 : e1;

                shorter.Kind = overlapping;
                longer.Kind = EdgeKind.NonContributing;
                Divide(longer, shorter.Other.Point, queue);
                return 2;
            }

            if (rightEqual)
            {
                // Same end: split the one that starts earlier at the other's start.
                var earlier = CompareEvents(e1, e2) < 0 ? e1 : e2;
                var later = ReferenceEquals(earlier, e1) ? e2 : e1;
                Divide(earlier, later.Point, queue);
                return 2;
            }

            // Fully staggered overlap: two cuts, one at each interior boundary.
            var startsFirst = CompareEvents(e1, e2) < 0 ? e1 : e2;
            var startsSecond = ReferenceEquals(startsFirst, e1) ? e2 : e1;
            var endsLast = CompareEvents(e1.Other, e2.Other) > 0 ? e1 : e2;

            Divide(startsFirst, startsSecond.Point, queue);
            Divide(endsLast, ReferenceEquals(endsLast, e1) ? e2.Other.Point : e1.Other.Point, queue);
            return 2;
        }

        /// <summary>Cuts one segment at an interior point, producing two events either side.</summary>
        /// <remarks>
        /// The new pieces are <see cref="EdgeKind.Normal"/>, never a copy of what was being cut.
        /// A collinear overlap is marked on the segment <em>before</em> it is divided here, and the
        /// mark describes only the overlapping stretch - so the piece left beyond the cut is an
        /// ordinary edge again. Copying the kind instead made the outer piece inherit
        /// "non-contributing" and vanish, which is how a union of two overlapping rectangles came
        /// back as one of them: the far side of the second rectangle was dropped before the result
        /// was ever assembled.
        /// </remarks>
        private static void Divide(SweepEvent e, Vector2 at, EventQueue queue)
        {
            var right = new SweepEvent
            {
                Point = at, Left = false, Other = e, Side = e.Side, Serial = e.Serial
            };
            var left = new SweepEvent
            {
                Point = at, Left = true, Other = e.Other, Side = e.Side, Serial = e.Other.Serial
            };

            // Splitting can reverse which endpoint the sweep meets first, and every comparison
            // below assumes Left is accurate - so it is re-established rather than assumed.
            if (CompareEvents(left, e.Other) > 0)
            {
                e.Other.Left = true;
                left.Left = false;
            }

            e.Other.Other = left;
            e.Other = right;

            queue.Push(left);
            queue.Push(right);
        }

        // ---- assembling the result ------------------------------------------

        private static List<List<Vector2>> ConnectEdges(List<SweepEvent> sorted)
        {
            // Only left events that survived, plus their partners, describe the result boundary.
            var events = new List<SweepEvent>();
            for (var i = 0; i < sorted.Count; i++)
            {
                var e = sorted[i];
                if ((e.Left && e.InResult) || (!e.Left && e.Other.InResult)) events.Add(e);
            }

            // One pass is not enough: the filtering above can reorder left/right pairs relative to
            // each other, and the walk below indexes partners by position.
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = 0; i < events.Count; i++)
                {
                    if (i + 1 < events.Count && CompareEvents(events[i], events[i + 1]) > 0)
                    {
                        (events[i], events[i + 1]) = (events[i + 1], events[i]);
                        changed = true;
                    }
                }
            }

            for (var i = 0; i < events.Count; i++) events[i].Position = i;

            // A right event's Position must point at its partner and vice versa: the walk below
            // crosses each segment by jumping to Position, then steps along the boundary.
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Left) continue;
                (events[i].Position, events[i].Other.Position) = (events[i].Other.Position, events[i].Position);
            }

            var result = new List<List<Vector2>>();
            var depths = new List<int>();
            var processed = new bool[events.Count];

            for (var i = 0; i < events.Count; i++)
            {
                if (processed[i]) continue;

                var ring = new List<Vector2> { events[i].Point };
                var contourId = result.Count;
                var depth = 0;

                // Whether this ring sits inside another decides if it is a hole, and the nearest
                // kept edge below is what knows. Its own transition direction distinguishes "I am
                // inside that ring" from "I am beside it".
                var lower = events[i].PrevInResult;
                if (lower != null && lower.ContourId >= 0 && lower.ContourId < depths.Count)
                    depth = lower.ResultInOut ? depths[lower.ContourId] : depths[lower.ContourId] + 1;

                var position = i;
                while (position >= i)
                {
                    processed[position] = true;

                    var owner = events[position].Left ? events[position] : events[position].Other;
                    owner.ResultInOut = !events[position].Left;
                    owner.ContourId = contourId;

                    position = events[position].Position;
                    if (position < 0 || position >= events.Count) break;

                    processed[position] = true;
                    ring.Add(events[position].Point);

                    position = NextPosition(events, processed, position, i);
                    if (position < 0) break;
                }

                if (ring.Count < 3) continue;

                // A hole must wind opposite whatever contains it, or the non-zero rule fills it in
                // instead of punching it out.
                var wantsPositive = depth % 2 == 0;
                if (NexPathFlattening.SignedDoubleArea(ring) > 0f != wantsPositive) ring.Reverse();

                result.Add(ring);
                depths.Add(depth);
            }

            return result;
        }

        /// <summary>
        /// The next event continuing the boundary from <paramref name="from"/>.
        /// </summary>
        /// <remarks>
        /// Several events share a point wherever edges meet, and the boundary continues along
        /// whichever of them has not been walked yet. Searching forward first and only then
        /// backward - never past where this ring started - is what keeps the walk on one ring
        /// instead of wandering into the next.
        /// </remarks>
        private static int NextPosition(List<SweepEvent> events, bool[] processed, int from, int origin)
        {
            var point = events[from].Point;

            for (var i = from + 1; i < events.Count && Same(events[i].Point, point); i++)
                if (!processed[i]) return i;

            for (var i = from - 1; i >= origin; i--)
                if (!processed[i]) return i;

            return -1;
        }

        // ---- geometry --------------------------------------------------------

        private static float SignedArea(Vector2 a, Vector2 b, Vector2 c)
            => (a.x - c.x) * (b.y - c.y) - (b.x - c.x) * (a.y - c.y);

        private static bool Same(Vector2 a, Vector2 b)
            => Mathf.Abs(a.x - b.x) < Epsilon && Mathf.Abs(a.y - b.y) < Epsilon;

        /// <summary>
        /// Where two segments meet.
        /// </summary>
        /// <returns>0 for nowhere, 1 for a single point in <paramref name="first"/>, 2 for a
        /// collinear overlap spanning <paramref name="first"/> to <paramref name="second"/>.</returns>
        private static int FindIntersection(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1,
            out Vector2 first, out Vector2 second)
        {
            first = default;
            second = default;

            var da = a1 - a0;
            var db = b1 - b0;
            var offset = b0 - a0;

            var cross = Cross(da, db);
            var lengthSquared = da.sqrMagnitude;

            if (Mathf.Abs(cross) > Epsilon * Mathf.Sqrt(lengthSquared * db.sqrMagnitude))
            {
                var s = Cross(offset, db) / cross;
                if (s < -Epsilon || s > 1f + Epsilon) return 0;

                var t = Cross(offset, da) / cross;
                if (t < -Epsilon || t > 1f + Epsilon) return 0;

                first = a0 + da * Mathf.Clamp01(s);
                return 1;
            }

            // Parallel. Collinear only if the offset between them is parallel too; otherwise the
            // segments are separated and never meet.
            if (Mathf.Abs(Cross(offset, da)) > Epsilon * Mathf.Sqrt(lengthSquared * offset.sqrMagnitude))
                return 0;

            if (lengthSquared < Epsilon * Epsilon) return 0;

            // Project both of b's endpoints onto a's parameter range and take the overlap.
            var t0 = Vector2.Dot(offset, da) / lengthSquared;
            var t1 = Vector2.Dot(b1 - a0, da) / lengthSquared;
            if (t0 > t1) (t0, t1) = (t1, t0);

            var low = Mathf.Max(0f, t0);
            var high = Mathf.Min(1f, t1);

            if (low > high + Epsilon) return 0;

            first = a0 + da * low;
            if (high - low < Epsilon) return 1;

            second = a0 + da * high;
            return 2;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Order the sweep visits endpoints in: left to right, then bottom to top.</summary>
        private static int CompareEvents(SweepEvent e1, SweepEvent e2)
        {
            if (e1.Point.x > e2.Point.x) return 1;
            if (e1.Point.x < e2.Point.x) return -1;
            if (e1.Point.y != e2.Point.y) return e1.Point.y > e2.Point.y ? 1 : -1;

            // Right endpoints first, so a segment is out of the status line before another one
            // starting at the same point is put in.
            if (e1.Left != e2.Left) return e1.Left ? 1 : -1;

            if (Mathf.Abs(SignedArea(e1.Point, e1.Other.Point, e2.Other.Point)) > Epsilon)
                return e1.Above(e2.Other.Point) ? 1 : -1;

            if (e1.Side != e2.Side) return e1.Side == PolygonSide.Subject ? -1 : 1;

            // Fully indistinguishable geometrically. Falling back to insertion order keeps the
            // comparator a strict weak ordering, which the sorts below rely on.
            return e1.Serial.CompareTo(e2.Serial);
        }

        /// <summary>Order of segments crossing the sweep line, bottom to top.</summary>
        private static int CompareSegments(SweepEvent e1, SweepEvent e2)
        {
            if (ReferenceEquals(e1, e2)) return 0;

            if (Mathf.Abs(SignedArea(e1.Point, e1.Other.Point, e2.Point)) > Epsilon ||
                Mathf.Abs(SignedArea(e1.Point, e1.Other.Point, e2.Other.Point)) > Epsilon)
            {
                if (Same(e1.Point, e2.Point)) return e1.Below(e2.Other.Point) ? -1 : 1;
                if (Mathf.Abs(e1.Point.x - e2.Point.x) < Epsilon) return e1.Point.y < e2.Point.y ? -1 : 1;
                if (CompareEvents(e1, e2) > 0) return e2.Above(e1.Point) ? -1 : 1;
                return e1.Below(e2.Point) ? -1 : 1;
            }

            if (e1.Side != e2.Side) return e1.Side == PolygonSide.Subject ? -1 : 1;
            if (Same(e1.Point, e2.Point)) return e1.Serial.CompareTo(e2.Serial);
            return CompareEvents(e1, e2);
        }

        // ---- containers ------------------------------------------------------

        /// <summary>
        /// A binary heap of pending endpoints.
        /// </summary>
        /// <remarks>
        /// A heap rather than a pre-sorted list because splitting a segment pushes new endpoints
        /// mid-sweep, and they can be earlier than events already queued.
        /// </remarks>
        private sealed class EventQueue
        {
            private readonly List<SweepEvent> _heap = new List<SweepEvent>();

            public int Count => _heap.Count;

            public void Push(SweepEvent e)
            {
                _heap.Add(e);
                var child = _heap.Count - 1;

                while (child > 0)
                {
                    var parent = (child - 1) / 2;
                    if (CompareEvents(_heap[child], _heap[parent]) >= 0) break;
                    (_heap[child], _heap[parent]) = (_heap[parent], _heap[child]);
                    child = parent;
                }
            }

            public SweepEvent Pop()
            {
                var top = _heap[0];
                var last = _heap.Count - 1;
                _heap[0] = _heap[last];
                _heap.RemoveAt(last);

                var parent = 0;
                while (true)
                {
                    var left = parent * 2 + 1;
                    if (left >= _heap.Count) break;

                    var smallest = left;
                    var right = left + 1;
                    if (right < _heap.Count && CompareEvents(_heap[right], _heap[left]) < 0) smallest = right;
                    if (CompareEvents(_heap[smallest], _heap[parent]) >= 0) break;

                    (_heap[smallest], _heap[parent]) = (_heap[parent], _heap[smallest]);
                    parent = smallest;
                }

                return top;
            }
        }

        /// <summary>
        /// Segments currently crossing the sweep line, kept in bottom-to-top order.
        /// </summary>
        /// <remarks>
        /// A sorted list rather than a balanced tree. The algorithm's every step asks for the
        /// neighbour above or below, which a list answers by index and a
        /// <see cref="System.Collections.Generic.SortedSet{T}"/> cannot answer at all without
        /// walking it. UI shapes have tens to hundreds of edges, so the linear insert is not what
        /// decides how fast this is.
        /// </remarks>
        private sealed class StatusLine
        {
            private readonly List<SweepEvent> _items = new List<SweepEvent>();

            public int Insert(SweepEvent e)
            {
                var index = _items.Count;
                for (var i = 0; i < _items.Count; i++)
                {
                    if (CompareSegments(e, _items[i]) < 0) { index = i; break; }
                }

                _items.Insert(index, e);
                return index;
            }

            public int IndexOf(SweepEvent e)
            {
                for (var i = 0; i < _items.Count; i++) if (ReferenceEquals(_items[i], e)) return i;
                return -1;
            }

            public void RemoveAt(int index)
            {
                if (index >= 0 && index < _items.Count) _items.RemoveAt(index);
            }

            public SweepEvent Below(int index) => index > 0 ? _items[index - 1] : null;

            public SweepEvent Above(int index) => index >= 0 && index + 1 < _items.Count ? _items[index + 1] : null;
        }
    }
}
