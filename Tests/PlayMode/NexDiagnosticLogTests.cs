using System;
using System.Linq;
using emiteat.NexUI.Diagnostics;
using NUnit.Framework;

namespace emiteat.NexUI.Tests.PlayMode
{
    /// <summary>
    /// Covers the session-wide diagnostic log the console reads.
    /// </summary>
    /// <remarks>
    /// The clock is injected so "first seen / last seen" can be asserted without waiting, which is
    /// the same trick the time source exists for elsewhere.
    /// </remarks>
    public sealed class NexDiagnosticLogTests
    {
        private DateTime _now;
        private NexDiagnosticLog _log;

        [SetUp]
        public void SetUp()
        {
            _now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
            _log = new NexDiagnosticLog(clock: () => _now);
        }

        private static NexDiagnostic Diagnostic(string code = "NEX-BND-4001", string screen = "MainMenu",
            string node = "n-1", NexSeverity severity = NexSeverity.Warning, string message = "boom")
            => new NexDiagnostic(code, severity, message, new NexSourceLocation(screen, node, "Root/Button"));

        // ---- grouping -------------------------------------------------------

        [Test]
        public void Record_CollapsesIdenticalReportsIntoOneEntryWithACount()
        {
            for (int i = 0; i < 5; i++) _log.Record(Diagnostic());

            Assert.AreEqual(1, _log.Count, "A rule firing repeatedly is one problem, not five rows.");
            Assert.AreEqual(5, _log.All().First().Occurrences);
        }

        [Test]
        public void Record_KeepsTheSameCodeOnDifferentElementsApart()
        {
            _log.Record(Diagnostic(node: "n-1"));
            _log.Record(Diagnostic(node: "n-1", message: "boom"));

            var other = new NexDiagnostic("NEX-BND-4001", NexSeverity.Warning, "boom",
                new NexSourceLocation("MainMenu", "n-2", "Root/Other"));
            _log.Record(other);

            Assert.AreEqual(2, _log.Count,
                "The same rule failing on two elements is two problems; collapsing hides one.");
        }

        [Test]
        public void Record_TracksFirstAndLastSeen()
        {
            _log.Record(Diagnostic());
            _now = _now.AddMinutes(10);
            _log.Record(Diagnostic());

            var entry = _log.All().First();
            Assert.AreEqual(12, entry.FirstSeen.Hour);
            Assert.AreEqual(10, entry.LastSeen.Minute, "'It started after I did X' is how people find a cause.");
        }

        // ---- bounded --------------------------------------------------------

        [Test]
        public void Record_DropsTheOldestOnceTheCapIsReached()
        {
            var log = new NexDiagnosticLog(capacity: 3, clock: () => _now);
            for (int i = 0; i < 5; i++) log.Record(Diagnostic(node: "n-" + i));

            Assert.AreEqual(3, log.Count, "A day-long session must not accumulate until memory runs out.");
        }

        // ---- filtering ------------------------------------------------------

        [Test]
        public void Query_FiltersBySeverity()
        {
            _log.Record(Diagnostic(severity: NexSeverity.Information, node: "a"));
            _log.Record(Diagnostic(severity: NexSeverity.Error, node: "b"));

            var errors = _log.Query(new NexDiagnosticQuery { MinSeverity = NexSeverity.Error }).ToList();

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(NexSeverity.Error, errors[0].Diagnostic.Severity);
        }

        [Test]
        public void Query_FiltersBySubsystemParsedFromTheCode()
        {
            _log.Record(Diagnostic(code: "NEX-BND-4001", node: "a"));
            _log.Record(Diagnostic(code: "NEX-DOC-1001", node: "b"));

            var bnd = _log.Query(new NexDiagnosticQuery
            {
                MinSeverity = NexSeverity.Trace, Subsystem = "BND"
            }).ToList();

            Assert.AreEqual(1, bnd.Count);
            Assert.AreEqual("BND", bnd[0].Subsystem);
        }

        [Test]
        public void Query_FiltersByScreen()
        {
            _log.Record(Diagnostic(screen: "MainMenu", node: "a"));
            _log.Record(Diagnostic(screen: "Store", node: "b"));

            var store = _log.Query(new NexDiagnosticQuery
            {
                MinSeverity = NexSeverity.Trace, ScreenId = "Store"
            }).ToList();

            Assert.AreEqual(1, store.Count);
        }

        [Test]
        public void Query_SearchesCodeMessageAndLocation()
        {
            _log.Record(Diagnostic(code: "NEX-BND-4001", message: "something odd", node: "a"));

            foreach (var needle in new[] { "bnd-4001", "ODD", "Root/Button" })
                Assert.AreEqual(1,
                    _log.Query(new NexDiagnosticQuery { MinSeverity = NexSeverity.Trace, Text = needle }).Count(),
                    "Search should match on '" + needle + "'.");
        }

        [Test]
        public void Query_ReturnsNewestFirst()
        {
            _log.Record(Diagnostic(node: "first"));
            _log.Record(Diagnostic(node: "second"));

            var ordered = _log.Query(new NexDiagnosticQuery { MinSeverity = NexSeverity.Trace }).ToList();

            Assert.AreEqual("second", ordered[0].Diagnostic.Location.NodeId);
        }

        // ---- resolved -------------------------------------------------------

        [Test]
        public void Resolved_EntriesAreHiddenUnlessAskedFor()
        {
            _log.Record(Diagnostic());
            var entry = _log.All().First();
            _log.SetResolved(entry, true);

            Assert.AreEqual(0, _log.Query(new NexDiagnosticQuery { MinSeverity = NexSeverity.Trace }).Count());
            Assert.AreEqual(1, _log.Query(new NexDiagnosticQuery
            {
                MinSeverity = NexSeverity.Trace, IncludeResolved = true
            }).Count());
        }

        [Test]
        public void Recurrence_UnresolvesAnEntry()
        {
            _log.Record(Diagnostic());
            _log.SetResolved(_log.All().First(), true);

            _log.Record(Diagnostic());

            Assert.IsFalse(_log.All().First().Resolved,
                "It came back, so it was not actually dealt with.");
        }

        [Test]
        public void CountAtLeast_IgnoresResolvedEntries()
        {
            _log.Record(Diagnostic(severity: NexSeverity.Error));
            Assert.AreEqual(1, _log.CountAtLeast(NexSeverity.Error));

            _log.SetResolved(_log.All().First(), true);
            Assert.AreEqual(0, _log.CountAtLeast(NexSeverity.Error));
        }

        // ---- export ---------------------------------------------------------

        [Test]
        public void ToJson_IncludesTheFieldsAReaderNeeds()
        {
            _log.Record(Diagnostic(code: "NEX-DOC-1003", message: "duplicate id"));

            var json = _log.ToJson(new NexDiagnosticQuery { MinSeverity = NexSeverity.Trace });

            StringAssert.Contains("\"code\": \"NEX-DOC-1003\"", json);
            StringAssert.Contains("\"subsystem\": \"DOC\"", json);
            StringAssert.Contains("\"screen\": \"MainMenu\"", json);
            StringAssert.Contains("\"occurrences\": 1", json);
        }

        [Test]
        public void ToJson_EscapesTextThatWouldBreakTheDocument()
        {
            _log.Record(new NexDiagnostic("NEX-DOC-1001", NexSeverity.Error,
                "he said \"stop\"\nand\\left"));

            var json = _log.ToJson(new NexDiagnosticQuery { MinSeverity = NexSeverity.Trace });

            StringAssert.Contains("\\\"stop\\\"", json);
            StringAssert.Contains("\\n", json);
            StringAssert.Contains("\\\\left", json);
        }

        [Test]
        public void ToJson_RecordsTheRootCauseWhenThereIsAChain()
        {
            var cause = new NexDiagnostic("NEX-DOC-1003", NexSeverity.Error, "duplicate id");
            _log.Record(cause.AsCauseOf("NEX-CMP-3001", NexSeverity.Error, "compile failed"));

            var json = _log.ToJson(new NexDiagnosticQuery { MinSeverity = NexSeverity.Trace });

            StringAssert.Contains("\"rootCauseCode\": \"NEX-DOC-1003\"", json);
        }

        // ---- catalog --------------------------------------------------------

        [Test]
        public void SubsystemOf_ParsesTheMiddleSegment()
        {
            Assert.AreEqual("BND", NexDiagnosticCodes.SubsystemOf("NEX-BND-4001"));
            Assert.AreEqual("TEST", NexDiagnosticCodes.SubsystemOf("NEX-TEST-9001"));
            Assert.AreEqual(string.Empty, NexDiagnosticCodes.SubsystemOf("nonsense"));
            Assert.AreEqual(string.Empty, NexDiagnosticCodes.SubsystemOf(null));
        }

        [Test]
        public void Subsystems_ListsEveryOneTheCatalogUsesWithoutDuplicates()
        {
            var subsystems = NexDiagnosticCodes.Subsystems().ToList();

            CollectionAssert.AllItemsAreUnique(subsystems);
            CollectionAssert.Contains(subsystems, "DOC");
            CollectionAssert.Contains(subsystems, "CMP");
            CollectionAssert.Contains(subsystems, "TEST");
        }

        [Test]
        public void Clear_EmptiesTheLog()
        {
            _log.Record(Diagnostic());
            _log.Clear();

            Assert.AreEqual(0, _log.Count);
        }
    }
}
