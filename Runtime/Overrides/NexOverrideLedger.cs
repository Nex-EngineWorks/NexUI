using System.Collections.Generic;
using System.Text;
using emiteat.NexUI.Compiled;

namespace emiteat.NexUI.Overrides
{
    /// <summary>A node property that something can change after the screen was built.</summary>
    public enum NexOverrideProperty
    {
        Text = 0,
        Visible = 1
    }

    /// <summary>Who changed a value.</summary>
    /// <remarks>
    /// Ordered from least to most specific intent: the authored value is what the screen was
    /// designed with, and everything below it is something deciding at runtime to differ.
    /// </remarks>
    public enum NexOverrideSource
    {
        /// <summary>The compiled value. Never recorded - it is the baseline everything else differs from.</summary>
        Authored = 0,

        /// <summary>A data binding pushed a value in.</summary>
        Binding = 1,

        /// <summary>An authored interaction rule set it.</summary>
        Interaction = 2,

        /// <summary>Game code set it directly.</summary>
        GameCode = 3
    }

    /// <summary>One recorded change to a node property.</summary>
    public struct NexOverrideRecord
    {
        public int NodeIndex;
        public NexOverrideProperty Property;
        public NexOverrideSource Source;

        /// <summary>The value that was written, rendered as text.</summary>
        public string Value;

        /// <summary>
        /// Which specific thing did it - a binding key, a rule id, a caller-supplied tag.
        /// Without this "an interaction changed it" is true but useless on a screen with twelve rules.
        /// </summary>
        public string Origin;

        /// <summary>Seconds on the screen's time source when it happened.</summary>
        public double At;
    }

    /// <summary>
    /// Remembers who last changed each node property, so the question "why does it say that?"
    /// has an answer.
    /// </summary>
    /// <remarks>
    /// This is the missing half of the debugging story. The source map already answers "which
    /// element is this object?"; a runtime value that disagrees with the authored one raises the
    /// next question immediately, and until now nothing could answer it - the author would see
    /// "Starting" where the document says "Start" and have to go read game code to find out why.
    ///
    /// Only the <em>last</em> writer per property is kept. A full history sounds better and is
    /// worse: it grows without bound in a long session, and the question people actually ask is
    /// "what is it now and who did that", not "list every write since the screen opened".
    ///
    /// The authored value is never recorded. It lives in the compiled program, which is immutable,
    /// so it is always available to compare against and cannot drift.
    /// </remarks>
    public sealed class NexOverrideLedger
    {
        private readonly NexScreenProgram _program;
        private readonly Time.INexTimeSource _time;
        private readonly Dictionary<long, NexOverrideRecord> _records = new Dictionary<long, NexOverrideRecord>();

        public NexOverrideLedger(NexScreenProgram program, Time.INexTimeSource time = null)
        {
            _program = program;
            _time = time ?? Time.NexTime.Default;
        }

        /// <summary>How many node properties currently differ from what was authored.</summary>
        public int Count => _records.Count;

        public IEnumerable<NexOverrideRecord> Records => _records.Values;

        /// <summary>Records that something changed a node property.</summary>
        public void Record(int nodeIndex, NexOverrideProperty property, NexOverrideSource source,
            string value, string origin)
        {
            if (nodeIndex < 0) return;

            _records[Key(nodeIndex, property)] = new NexOverrideRecord
            {
                NodeIndex = nodeIndex,
                Property = property,
                Source = source,
                Value = value ?? string.Empty,
                Origin = origin ?? string.Empty,
                At = _time.Now
            };
        }

        public bool TryGet(int nodeIndex, NexOverrideProperty property, out NexOverrideRecord record)
            => _records.TryGetValue(Key(nodeIndex, property), out record);

        /// <summary>True when this property still holds the value the screen was authored with.</summary>
        public bool IsAuthored(int nodeIndex, NexOverrideProperty property)
            => !_records.ContainsKey(Key(nodeIndex, property));

        /// <summary>Forgets an override, e.g. after game code restores the authored value.</summary>
        public void Clear(int nodeIndex, NexOverrideProperty property)
            => _records.Remove(Key(nodeIndex, property));

        public void Clear() => _records.Clear();

        /// <summary>
        /// Answers "why does this say what it says?" in one readable block.
        /// </summary>
        /// <remarks>
        /// Written for the person who authored the screen, so it names the element by its
        /// authoring path and always states the authored value - the comparison is the whole point.
        /// </remarks>
        public string Explain(int nodeIndex, NexOverrideProperty property)
        {
            var path = PathOf(nodeIndex);
            var authored = AuthoredValue(nodeIndex, property);

            if (!TryGet(nodeIndex, property, out var record))
                return path + "." + property + " = " + Quote(authored) +
                       "\n  authored; nothing has changed it.";

            var sb = new StringBuilder();
            sb.Append(path).Append('.').Append(property).Append(" = ").Append(Quote(record.Value));
            sb.Append("\n  set by ").Append(record.Source);

            if (!string.IsNullOrEmpty(record.Origin)) sb.Append(" '").Append(record.Origin).Append('\'');
            sb.Append(" at ").Append(record.At.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append('s');
            sb.Append("\n  authored value was ").Append(Quote(authored)).Append('.');

            return sb.ToString();
        }

        /// <summary>Every property that currently differs from the document, for a debugger list.</summary>
        public string ExplainAll()
        {
            if (_records.Count == 0) return "Nothing on this screen differs from the document.";

            var sb = new StringBuilder();
            foreach (var record in _records.Values)
                sb.Append(Explain(record.NodeIndex, record.Property)).Append("\n\n");
            return sb.ToString().TrimEnd();
        }

        // ---- helpers --------------------------------------------------------

        /// <summary>
        /// Node index and property packed into one key.
        /// </summary>
        /// <remarks>
        /// A long rather than a tuple or a composed string: this is written on every binding push,
        /// so it must not allocate. Node indices are bounded by the screen's node count, far below
        /// the 32 bits given to them here.
        /// </remarks>
        private static long Key(int nodeIndex, NexOverrideProperty property)
            => ((long)nodeIndex << 8) | (byte)property;

        private string AuthoredValue(int nodeIndex, NexOverrideProperty property)
        {
            if (_program == null || nodeIndex < 0 || nodeIndex >= _program.Nodes.Length) return string.Empty;

            var node = _program.Nodes[nodeIndex];
            return property == NexOverrideProperty.Text
                ? node.Text ?? string.Empty
                : node.Visible ? "true" : "false";
        }

        private string PathOf(int nodeIndex)
        {
            if (_program == null || nodeIndex < 0 || nodeIndex >= _program.Nodes.Length) return "<unknown>";

            var path = _program.SourceMap.PathOfIndex(nodeIndex);
            return !string.IsNullOrEmpty(path) ? path : _program.Nodes[nodeIndex].Name;
        }

        private static string Quote(string value) => "'" + (value ?? string.Empty) + "'";
    }
}
