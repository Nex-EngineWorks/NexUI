using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace emiteat.NexUI.Diagnostics
{
    /// <summary>One diagnostic as the console shows it: the report plus how often it happened.</summary>
    public sealed class NexDiagnosticEntry
    {
        public NexDiagnostic Diagnostic { get; }

        /// <summary>How many times this exact diagnostic was reported.</summary>
        public int Occurrences { get; internal set; }

        public DateTime FirstSeen { get; }
        public DateTime LastSeen { get; internal set; }

        /// <summary>Marked by the user as dealt with. Survives until the next time it recurs.</summary>
        public bool Resolved { get; internal set; }

        public string Subsystem => NexDiagnosticCodes.SubsystemOf(Diagnostic.Code);

        internal NexDiagnosticEntry(NexDiagnostic diagnostic, DateTime now)
        {
            Diagnostic = diagnostic;
            Occurrences = 1;
            FirstSeen = now;
            LastSeen = now;
        }
    }

    /// <summary>What to show. Every field is optional; an empty query matches everything.</summary>
    public struct NexDiagnosticQuery
    {
        /// <summary>Hide anything below this. Defaults to <see cref="NexSeverity.Trace"/> - show all.</summary>
        public NexSeverity MinSeverity;

        /// <summary>Subsystem segment, e.g. <c>BND</c>. Empty matches all.</summary>
        public string Subsystem;

        /// <summary>Screen id. Empty matches all.</summary>
        public string ScreenId;

        /// <summary>Substring matched against code, message and location.</summary>
        public string Text;

        /// <summary>When false, entries the user marked resolved are hidden.</summary>
        public bool IncludeResolved;

        public bool Matches(NexDiagnosticEntry entry)
        {
            if (entry?.Diagnostic == null) return false;
            if (entry.Diagnostic.Severity < MinSeverity) return false;
            if (!IncludeResolved && entry.Resolved) return false;

            if (!string.IsNullOrEmpty(Subsystem) &&
                !string.Equals(entry.Subsystem, Subsystem, StringComparison.OrdinalIgnoreCase)) return false;

            if (!string.IsNullOrEmpty(ScreenId) &&
                !string.Equals(entry.Diagnostic.Location.ScreenId, ScreenId, StringComparison.Ordinal)) return false;

            if (string.IsNullOrEmpty(Text)) return true;

            return Contains(entry.Diagnostic.Code, Text)
                   || Contains(entry.Diagnostic.Message, Text)
                   || Contains(entry.Diagnostic.Location.ToString(), Text);
        }

        private static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack) &&
               haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Collects diagnostics across operations so there is one place to look.
    /// </summary>
    /// <remarks>
    /// A <see cref="NexDiagnosticBag"/> belongs to one compile or one screen load and is thrown
    /// away with it. That is right for deciding whether an operation succeeded, and useless for
    /// the question a person actually has, which is "what is wrong with my project right now".
    /// The log is the session-scoped answer.
    ///
    /// Identical diagnostics collapse into one entry with a count, for the same reason the bag
    /// does it: a rule that fires on 200 nodes is one problem, not 200 rows to scroll past. Unlike
    /// the bag, the log also keeps first- and last-seen times, because "this started happening
    /// after I did X" is how people locate a cause.
    ///
    /// Bounded. A session left open for a day must not accumulate diagnostics until the editor
    /// runs out of memory, so the oldest entries are dropped once the cap is reached.
    /// </remarks>
    public sealed class NexDiagnosticLog
    {
        /// <summary>Distinct entries kept. Occurrences of an existing entry are always counted.</summary>
        public const int DefaultCapacity = 512;

        private readonly Dictionary<string, NexDiagnosticEntry> _byKey =
            new Dictionary<string, NexDiagnosticEntry>(StringComparer.Ordinal);

        private readonly List<string> _order = new List<string>();
        private readonly Func<DateTime> _clock;

        public int Capacity { get; }

        public int Count => _order.Count;

        /// <summary>Raised whenever the log changes, so a window can repaint without polling.</summary>
        public event Action Changed;

        /// <param name="clock">Injectable for tests; defaults to the local wall clock.</param>
        public NexDiagnosticLog(int capacity = DefaultCapacity, Func<DateTime> clock = null)
        {
            Capacity = capacity < 1 ? 1 : capacity;
            _clock = clock ?? (() => DateTime.Now);
        }

        /// <summary>Records a diagnostic, or counts another occurrence of one already present.</summary>
        public void Record(NexDiagnostic diagnostic)
        {
            if (diagnostic == null) return;

            var key = KeyOf(diagnostic);
            var now = _clock();

            if (_byKey.TryGetValue(key, out var existing))
            {
                existing.Occurrences++;
                existing.LastSeen = now;

                // It came back, so it is not resolved after all.
                existing.Resolved = false;

                Changed?.Invoke();
                return;
            }

            _byKey[key] = new NexDiagnosticEntry(diagnostic, now);
            _order.Add(key);

            while (_order.Count > Capacity)
            {
                _byKey.Remove(_order[0]);
                _order.RemoveAt(0);
            }

            Changed?.Invoke();
        }

        /// <summary>Records everything in a bag - the usual bridge from a compile or a screen load.</summary>
        public void RecordAll(IEnumerable<NexDiagnostic> diagnostics)
        {
            if (diagnostics == null) return;
            foreach (var diagnostic in diagnostics) Record(diagnostic);
        }

        /// <summary>Entries matching a query, newest first.</summary>
        public IEnumerable<NexDiagnosticEntry> Query(NexDiagnosticQuery query)
        {
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                if (!_byKey.TryGetValue(_order[i], out var entry)) continue;
                if (query.Matches(entry)) yield return entry;
            }
        }

        public IEnumerable<NexDiagnosticEntry> All() => Query(new NexDiagnosticQuery
        {
            MinSeverity = NexSeverity.Trace,
            IncludeResolved = true
        });

        /// <summary>Screens that appear in the log, for a filter dropdown.</summary>
        public IEnumerable<string> ScreenIds()
        {
            var seen = new List<string>();
            foreach (var entry in All())
            {
                var screenId = entry.Diagnostic.Location.ScreenId;
                if (!string.IsNullOrEmpty(screenId) && !seen.Contains(screenId)) seen.Add(screenId);
            }
            return seen;
        }

        public int CountAtLeast(NexSeverity severity)
        {
            var count = 0;
            foreach (var entry in All())
                if (entry.Diagnostic.Severity >= severity && !entry.Resolved) count++;
            return count;
        }

        /// <summary>Marks an entry dealt with. It un-resolves itself if the diagnostic recurs.</summary>
        public void SetResolved(NexDiagnosticEntry entry, bool resolved)
        {
            if (entry == null) return;
            entry.Resolved = resolved;
            Changed?.Invoke();
        }

        public void Clear()
        {
            _byKey.Clear();
            _order.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// The log as JSON, for attaching to a bug report or reading in CI.
        /// </summary>
        /// <remarks>
        /// Hand-written rather than via <c>JsonUtility</c>: this type is engine-free by design, and
        /// the output shape should be stable for whatever reads it rather than following whatever
        /// Unity's serializer does with the fields.
        /// </remarks>
        public string ToJson(NexDiagnosticQuery query = default)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"diagnostics\": [\n");

            var first = true;
            foreach (var entry in Query(query))
            {
                if (!first) sb.Append(",\n");
                first = false;

                var d = entry.Diagnostic;
                sb.Append("    {");
                sb.Append("\"code\": ").Append(Json(d.Code));
                sb.Append(", \"severity\": ").Append(Json(d.Severity.ToString()));
                sb.Append(", \"subsystem\": ").Append(Json(entry.Subsystem));
                sb.Append(", \"message\": ").Append(Json(d.Message));
                sb.Append(", \"screen\": ").Append(Json(d.Location.ScreenId));
                sb.Append(", \"node\": ").Append(Json(d.Location.NodeId));
                sb.Append(", \"path\": ").Append(Json(d.Location.NodePath));
                sb.Append(", \"occurrences\": ").Append(entry.Occurrences);
                sb.Append(", \"firstSeen\": ").Append(Json(entry.FirstSeen.ToString("o", CultureInfo.InvariantCulture)));
                sb.Append(", \"lastSeen\": ").Append(Json(entry.LastSeen.ToString("o", CultureInfo.InvariantCulture)));
                sb.Append(", \"resolved\": ").Append(entry.Resolved ? "true" : "false");

                var root = d.RootCause();
                if (!ReferenceEquals(root, d))
                {
                    sb.Append(", \"rootCauseCode\": ").Append(Json(root.Code));
                    sb.Append(", \"rootCauseMessage\": ").Append(Json(root.Message));
                }

                sb.Append('}');
            }

            sb.Append("\n  ]\n}");
            return sb.ToString();
        }

        /// <summary>
        /// Groups identical reports. Location is part of the key: the same rule failing on two
        /// different elements is two problems, and collapsing them would hide one of them.
        /// </summary>
        private static string KeyOf(NexDiagnostic diagnostic)
            => diagnostic.Code + "|" + diagnostic.Location + "|" + diagnostic.Message;

        private static string Json(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";

            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
