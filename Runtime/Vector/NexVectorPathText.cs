using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace emiteat.NexUI.Vector
{
    /// <summary>
    /// Reads and writes a path as SVG path-data text.
    /// </summary>
    /// <remarks>
    /// Exists because UXML attributes are text. A path is a list of points with control handles,
    /// which no ordinary attribute can hold, so without a text form a generated UXML file can only
    /// describe an empty vector element - which is exactly what it used to do.
    ///
    /// SVG's <c>d</c> grammar rather than a private format, restricted to the four commands a
    /// NexUI path can produce. It costs nothing over a custom encoding and buys a great deal: the
    /// output pastes straight into any drawing tool, and a designer handing over an icon can be
    /// answered in the notation they already use.
    ///
    /// The parser is hand-written and lives in this assembly on purpose. Deferring to
    /// <c>Unity.VectorGraphics</c> would make reading back a generated UXML file impossible on
    /// 2022.3, where that module does not exist - see <see cref="NexVectorTessellator.IsSupported"/>.
    /// Only the subset <see cref="Encode"/> emits is understood; anything richer belongs in the
    /// full SVG importer.
    /// </remarks>
    public static class NexVectorPathText
    {
        /// <summary>Round-trips through <see cref="float"/> without widening the text needlessly.</summary>
        private const string Format = "0.####";

        /// <summary>Writes a shape's contours as SVG path data.</summary>
        public static string Encode(NexVectorShape shape)
        {
            if (shape == null) return string.Empty;

            var text = new StringBuilder();

            for (var c = 0; c < shape.Contours.Count; c++)
            {
                var contour = shape.Contours[c];
                if (contour == null || contour.Anchors.Count < 2) continue;

                var anchors = contour.Anchors;
                if (text.Length > 0) text.Append(' ');

                text.Append('M').Append(' ').Append(Point(anchors[0].Position));

                var segments = contour.Closed ? anchors.Count : anchors.Count - 1;
                for (var i = 0; i < segments; i++)
                {
                    var from = anchors[i];
                    var to = anchors[(i + 1) % anchors.Count];

                    // A segment with no handles is a straight line, and writing it as one keeps a
                    // rectangle four short commands instead of four cubics with redundant controls.
                    if (from.OutHandle == Vector2.zero && to.InHandle == Vector2.zero)
                    {
                        // The closing segment of a closed contour is implied by Z, so emitting a
                        // line back to the start would leave a duplicate point on re-read.
                        if (contour.Closed && i == segments - 1) continue;

                        text.Append(" L ").Append(Point(to.Position));
                        continue;
                    }

                    text.Append(" C ")
                        .Append(Point(from.Position + from.OutHandle)).Append(' ')
                        .Append(Point(to.Position + to.InHandle)).Append(' ')
                        .Append(Point(to.Position));
                }

                if (contour.Closed) text.Append(" Z");
            }

            return text.ToString();
        }

        /// <summary>
        /// Reads SVG path data back into contours. Returns an empty shape for empty or unusable text.
        /// </summary>
        /// <remarks>
        /// Deliberately forgiving: unknown commands are skipped rather than throwing. This runs
        /// while a UXML file is being loaded, where an exception takes the whole document down and
        /// a missing shape takes down one element.
        /// </remarks>
        public static NexVectorShape Decode(string pathData)
        {
            var shape = new NexVectorShape();
            if (string.IsNullOrWhiteSpace(pathData)) return shape;

            var tokens = Tokenize(pathData);
            NexVectorContour contour = null;
            var cursor = Vector2.zero;
            var index = 0;

            while (index < tokens.Count)
            {
                var command = tokens[index++];
                if (command.Length != 1) continue;

                switch (char.ToUpperInvariant(command[0]))
                {
                    case 'M':
                        if (!TryPoint(tokens, ref index, out var start)) return shape;
                        contour = new NexVectorContour { Closed = false };
                        shape.Contours.Add(contour);
                        contour.Anchors.Add(new NexVectorAnchor(start));
                        cursor = start;
                        break;

                    case 'L':
                        if (contour == null || !TryPoint(tokens, ref index, out var line)) return shape;
                        contour.Anchors.Add(new NexVectorAnchor(line));
                        cursor = line;
                        break;

                    case 'C':
                        if (contour == null ||
                            !TryPoint(tokens, ref index, out var control1) ||
                            !TryPoint(tokens, ref index, out var control2) ||
                            !TryPoint(tokens, ref index, out var end))
                        {
                            return shape;
                        }

                        // Controls are absolute in the text and relative to their anchor in the
                        // model, and the outgoing one belongs to the point already placed - so the
                        // previous anchor is rewritten rather than the new one carrying both.
                        var previous = contour.Anchors[contour.Anchors.Count - 1];
                        previous.OutHandle = control1 - previous.Position;
                        contour.Anchors[contour.Anchors.Count - 1] = previous;

                        contour.Anchors.Add(new NexVectorAnchor(end, control2 - end));
                        cursor = end;
                        break;

                    case 'Z':
                        if (contour == null) break;
                        contour.Closed = true;

                        // A path written "... L 0 0 Z" repeats its first point; the model expresses
                        // the closing segment with the flag, so the duplicate has to go or the pen
                        // tool inherits an anchor sitting exactly on another.
                        var anchors = contour.Anchors;
                        if (anchors.Count > 2 &&
                            (anchors[anchors.Count - 1].Position - anchors[0].Position).sqrMagnitude < 1e-8f)
                        {
                            // The last point's incoming handle belongs to the first point now.
                            var first = anchors[0];
                            first.InHandle = anchors[anchors.Count - 1].InHandle;
                            anchors[0] = first;
                            anchors.RemoveAt(anchors.Count - 1);
                        }

                        contour = null;
                        cursor = anchors.Count > 0 ? anchors[0].Position : cursor;
                        break;
                }
            }

            return shape;
        }

        private static string Point(Vector2 point)
            => point.x.ToString(Format, CultureInfo.InvariantCulture) + " " +
               point.y.ToString(Format, CultureInfo.InvariantCulture);

        private static bool TryPoint(List<string> tokens, ref int index, out Vector2 point)
        {
            point = default;
            if (index + 1 >= tokens.Count) return false;

            if (!float.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(tokens[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return false;
            }

            index += 2;
            point = new Vector2(x, y);
            return true;
        }

        /// <summary>
        /// Splits path data into commands and numbers.
        /// </summary>
        /// <remarks>
        /// Commas and whitespace separate equally, and a command letter needs no separator at all -
        /// both are ordinary in SVG, and text written by another tool is exactly the case worth
        /// accepting.
        /// </remarks>
        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();

            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];

                if (char.IsLetter(character))
                {
                    if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                    tokens.Add(character.ToString());
                    continue;
                }

                if (char.IsWhiteSpace(character) || character == ',')
                {
                    if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                    continue;
                }

                // A minus sign starts a new number unless it is an exponent's sign, which is the
                // one place "3e-4" would otherwise be split into two tokens.
                if (character == '-' && current.Length > 0 &&
                    current[current.Length - 1] != 'e' && current[current.Length - 1] != 'E')
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                current.Append(character);
            }

            if (current.Length > 0) tokens.Add(current.ToString());
            return tokens;
        }
    }
}
