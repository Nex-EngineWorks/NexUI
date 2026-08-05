using System;
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
        private readonly List<NexDiagnosticContext> _scopes = new List<NexDiagnosticContext>();

        /// <summary>
        /// Feature / origin / handler stamped onto anything added right now.
        /// </summary>
        /// <remarks>
        /// Innermost scope wins per field, inheriting whatever it leaves unset - see
        /// <see cref="NexDiagnosticContext.InheritingFrom"/>.
        /// </remarks>
        public NexDiagnosticContext CurrentContext =>
            _scopes.Count == 0 ? NexDiagnosticContext.None : _scopes[_scopes.Count - 1];

        /// <summary>
        /// Attributes everything added inside the returned scope to a feature and a route.
        /// </summary>
        /// <remarks>
        /// The alternative was passing feature / origin / handler at every <c>Add</c>, across
        /// dozens of call sites, where the arguments would be forgotten on exactly the new checks
        /// nobody has debugged yet. A pass should say what is wrong; which user action it belongs
        /// to is the caller's knowledge, and this is how the caller supplies it once.
        ///
        /// <code>
        /// using (bag.Scope(NexFeatures.UGuiSave, origin: nameof(UGUIAssetSerializer)))
        /// using (bag.Scope(handler: element.elementId))
        ///     writer.Apply(...);   // every diagnostic in here is attributed
        /// </code>
        /// </remarks>
        public IDisposable Scope(string feature = null, string origin = null, string handler = null,
            string operationId = null)
        {
            var incoming = new NexDiagnosticContext(feature, origin, handler, operationId);
            _scopes.Add(incoming.InheritingFrom(CurrentContext));
            return new ScopeHandle(this, _scopes.Count);
        }

        /// <summary>
        /// Pops by depth, not by reference.
        /// </summary>
        /// <remarks>
        /// Disposing out of order - which a stray <c>using</c> or an early return can cause - would
        /// otherwise silently leave a scope on the stack and misattribute everything after it.
        /// Truncating to the recorded depth makes a mis-nested dispose self-correcting.
        /// </remarks>
        private sealed class ScopeHandle : IDisposable
        {
            private readonly NexDiagnosticBag _bag;
            private readonly int _depth;
            private bool _disposed;

            public ScopeHandle(NexDiagnosticBag bag, int depth)
            {
                _bag = bag;
                _depth = depth;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_bag._scopes.Count >= _depth)
                    _bag._scopes.RemoveRange(_depth - 1, _bag._scopes.Count - _depth + 1);
            }
        }

        /// <summary>Highest severity seen so far; <see cref="NexSeverity.Trace"/> when empty.</summary>
        public NexSeverity MaxSeverity { get; private set; } = NexSeverity.Trace;

        public int Count => _items.Count;

        public bool HasErrors => MaxSeverity >= NexSeverity.Error;

        public IReadOnlyList<NexDiagnostic> Items => _items;

        public NexDiagnostic Add(NexDiagnostic diagnostic)
        {
            if (diagnostic == null) return null;

            // A diagnostic that already carries a context keeps it: it was raised somewhere else
            // and handed here, and re-stamping would credit it to whatever scope is open now.
            var stamped = diagnostic.Context.IsNone
                ? diagnostic.WithContext(CurrentContext)
                : diagnostic;

            // The feature is part of the key. The same code at the same element genuinely is two
            // problems when one comes from an import and the other from a save, and collapsing
            // them would hide whichever happened second.
            var key = stamped.Code + "|" + stamped.Location + "|" + stamped.Message
                      + "|" + stamped.Context.Feature;
            if (_occurrences.TryGetValue(key, out var count))
            {
                _occurrences[key] = count + 1;
                return stamped; // Already reported for this exact location; keep the first.
            }

            _occurrences[key] = 1;
            _items.Add(stamped);
            if (stamped.Severity > MaxSeverity) MaxSeverity = stamped.Severity;
            return stamped;
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
            var key = diagnostic.Code + "|" + diagnostic.Location + "|" + diagnostic.Message
                      + "|" + diagnostic.Context.Feature;
            return _occurrences.TryGetValue(key, out var count) ? count : 0;
        }

        /// <summary>Everything raised by one feature. Pass null or empty for the unattributed ones.</summary>
        public IEnumerable<NexDiagnostic> ForFeature(string feature)
        {
            for (int i = 0; i < _items.Count; i++)
                if (SameFeature(_items[i].Context.Feature, feature))
                    yield return _items[i];
        }

        /// <summary>
        /// Feature comparison that treats null and empty as the same thing.
        /// </summary>
        /// <remarks>
        /// <see cref="NexDiagnosticContext.None"/> is <c>default</c>, so it never runs the
        /// constructor that coerces nulls to empty strings - its <c>Feature</c> is null, not "".
        /// A plain <c>string.Equals(null, "")</c> is false, which silently dropped every
        /// unattributed diagnostic out of the "Uncategorized" group rather than showing it.
        /// </remarks>
        private static bool SameFeature(string a, string b)
            => string.IsNullOrEmpty(a) ? string.IsNullOrEmpty(b) : string.Equals(a, b, StringComparison.Ordinal);

        /// <summary>Features that raised something, in the order they first did.</summary>
        public IEnumerable<string> Features()
        {
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < _items.Count; i++)
            {
                var feature = _items[i].Context.Feature;
                if (!string.IsNullOrEmpty(feature) && seen.Add(feature)) yield return feature;
            }
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

        /// <summary>
        /// Human-readable summary, grouped by feature, with the route that raised each line.
        /// </summary>
        /// <remarks>
        /// Grouped rather than chronological because the first question about a failed operation is
        /// which part of it failed. A flat list of twelve lines from three features reads as twelve
        /// unrelated problems; the same lines under three headings read as "the save worked, the
        /// import did not".
        ///
        /// Diagnostics raised without a scope collect under a final "Uncategorized" heading rather
        /// than being dropped, so a check that nobody attributed is visible instead of invisible.
        /// </remarks>
        public string Format(NexSeverity minimum = NexSeverity.Information)
        {
            var sb = new StringBuilder();
            var written = new bool[_items.Count];

            foreach (var feature in Features())
                WriteGroup(sb, feature, minimum, written);

            WriteGroup(sb, string.Empty, minimum, written);
            return sb.ToString();
        }

        private void WriteGroup(StringBuilder sb, string feature, NexSeverity minimum, bool[] written)
        {
            var headerWritten = false;

            for (int i = 0; i < _items.Count; i++)
            {
                if (written[i]) continue;

                var item = _items[i];
                if (item.Severity < minimum) continue;
                if (!SameFeature(item.Context.Feature, feature)) continue;

                if (!headerWritten)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append("== ").Append(string.IsNullOrEmpty(feature) ? "Uncategorized" : feature)
                      .Append(" ==\n");
                    headerWritten = true;
                }

                written[i] = true;
                sb.Append(item.Severity).Append("  ").Append(item);

                var route = item.Context.Route();
                if (!string.IsNullOrEmpty(route)) sb.Append("  [").Append(route).Append(']');

                var repeats = OccurrenceCount(item);
                if (repeats > 1) sb.Append("  (+").Append(repeats - 1).Append(" more)");
                sb.Append('\n');
            }
        }

        public void Clear()
        {
            _items.Clear();
            _occurrences.Clear();
            _scopes.Clear();
            MaxSeverity = NexSeverity.Trace;
        }

        public IEnumerator<NexDiagnostic> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
