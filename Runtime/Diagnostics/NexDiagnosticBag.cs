using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace emiteat.NexUI.Diagnostics
{
    /// <summary>
    /// Collects diagnostics for one operation - a compile, a publish, a screen load - so a pass
    /// can report every problem it finds instead of aborting on the first one.
    /// </summary>
    /// <remarks>
    /// Not thread-safe by design: one bag belongs to one operation on one thread, and the
    /// background job scheduler hands the finished bag back to the main thread rather than
    /// letting two threads append to it.
    ///
    /// Duplicate suppression is deliberate. A pass that walks 500 nodes and hits the same missing
    /// component type on each one should produce one diagnostic with a count, not 500 rows the
    /// user has to scroll past.
    /// </remarks>
    public sealed class NexDiagnosticBag : IEnumerable<NexDiagnostic>
    {
        private readonly List<NexDiagnostic> _items = new List<NexDiagnostic>();
        private readonly Dictionary<string, int> _occurrences = new Dictionary<string, int>();

        /// <summary>Highest severity seen so far; <see cref="NexSeverity.Trace"/> when empty.</summary>
        public NexSeverity MaxSeverity { get; private set; } = NexSeverity.Trace;

        public int Count => _items.Count;

        public bool HasErrors => MaxSeverity >= NexSeverity.Error;

        public IReadOnlyList<NexDiagnostic> Items => _items;

        public NexDiagnostic Add(NexDiagnostic diagnostic)
        {
            if (diagnostic == null) return null;

            var key = diagnostic.Code + "|" + diagnostic.Location + "|" + diagnostic.Message;
            if (_occurrences.TryGetValue(key, out var count))
            {
                _occurrences[key] = count + 1;
                return diagnostic; // Already reported for this exact location; keep the first.
            }

            _occurrences[key] = 1;
            _items.Add(diagnostic);
            if (diagnostic.Severity > MaxSeverity) MaxSeverity = diagnostic.Severity;
            return diagnostic;
        }

        /// <summary>Adds a catalogued diagnostic. See <see cref="NexDiagnosticCodes.Create"/>.</summary>
        public NexDiagnostic Add(string code, NexSourceLocation location = default,
            string message = null, string detail = null, NexSeverity? severity = null,
            NexDiagnostic cause = null)
            => Add(NexDiagnosticCodes.Create(code, location, message, detail, severity, cause));

        /// <summary>How many times an identical diagnostic was suppressed after the first.</summary>
        public int OccurrenceCount(NexDiagnostic diagnostic)
        {
            if (diagnostic == null) return 0;
            var key = diagnostic.Code + "|" + diagnostic.Location + "|" + diagnostic.Message;
            return _occurrences.TryGetValue(key, out var count) ? count : 0;
        }

        /// <summary>The first diagnostic at or above <see cref="NexSeverity.Error"/>, or null.</summary>
        public NexDiagnostic FirstError()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Severity >= NexSeverity.Error) return _items[i];
            return null;
        }

        public IEnumerable<NexDiagnostic> AtLeast(NexSeverity severity)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Severity >= severity) yield return _items[i];
        }

        /// <summary>Human-readable summary, one line per diagnostic, with suppression counts.</summary>
        public string Format(NexSeverity minimum = NexSeverity.Information)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Severity < minimum) continue;

                sb.Append(item.Severity).Append("  ").Append(item);

                var repeats = OccurrenceCount(item);
                if (repeats > 1) sb.Append("  (+").Append(repeats - 1).Append(" more)");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public void Clear()
        {
            _items.Clear();
            _occurrences.Clear();
            MaxSeverity = NexSeverity.Trace;
        }

        public IEnumerator<NexDiagnostic> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
