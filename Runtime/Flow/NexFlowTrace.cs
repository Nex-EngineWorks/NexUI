using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace emiteat.NexUI.Flow
{
    /// <summary>
    /// Records what happened between an input and the last handler it reached.
    /// </summary>
    /// <remarks>
    /// Off by default, including in development builds. Tracing is a debugging tool that costs
    /// allocations and time, and a profiler that changes the thing it measures is worse than no
    /// profiler - so it is opt-in per session and its own overhead is reported alongside the
    /// results (see <see cref="OverheadMs"/>).
    ///
    /// Defining <c>NEXUI_DISABLE_FLOW_TRACE</c> compiles the recording away entirely for a
    /// shipping build; what remains is one enum comparison at each call site, which the JIT
    /// folds away. Without the define, leaving <see cref="Level"/> at
    /// <see cref="NexFlowLevel.Off"/> costs the same comparison and allocates nothing.
    /// </remarks>
    public static class NexFlowTrace
    {
        private static readonly List<INexFlowSink> _sinks = new List<INexFlowSink>();
        private static readonly Stack<NexFlowRecord> _pool = new Stack<NexFlowRecord>();
        private static double _overheadMs;

#if NEXUI_DISABLE_FLOW_TRACE
        /// <summary>Always Off in this build: NEXUI_DISABLE_FLOW_TRACE is defined.</summary>
        public static NexFlowLevel Level
        {
            get => NexFlowLevel.Off;
            set { /* compiled out */ }
        }
#else
        public static NexFlowLevel Level { get; set; } = NexFlowLevel.Off;
#endif

        /// <summary>True when a trace would actually be recorded. Check before building expensive detail strings.</summary>
        public static bool IsEnabled => Level != NexFlowLevel.Off;

        /// <summary>
        /// Time spent inside the tracer itself since the last <see cref="ResetOverhead"/>.
        /// Surfaced in the performance panel so instrumentation cost is never mistaken for UI cost.
        /// </summary>
        public static double OverheadMs => _overheadMs;

        public static void AddSink(INexFlowSink sink)
        {
            if (sink != null && !_sinks.Contains(sink)) _sinks.Add(sink);
        }

        public static void RemoveSink(INexFlowSink sink) => _sinks.Remove(sink);

        public static void ClearSinks() => _sinks.Clear();

        public static void ResetOverhead() => _overheadMs = 0d;

        /// <summary>
        /// Starts recording an interaction. Always dispose the returned scope - use a
        /// <c>using</c> - or the record is never emitted and its buffer never returns to the pool.
        /// </summary>
        /// <param name="origin">
        /// Authoring path of the element the interaction started at. Pass what the author would
        /// recognise (<c>MainMenu/StartButton</c>), not a GameObject instance id.
        /// </param>
        public static NexFlowScope Begin(string origin)
        {
            if (Level == NexFlowLevel.Off) return default;

            var start = Stopwatch.GetTimestamp();
            var record = _pool.Count > 0 ? _pool.Pop() : new NexFlowRecord();
            record.Reset();
            record.Origin = origin;
            record.StartedAt = DateTime.Now;

            _overheadMs += ElapsedMs(start);

            var began = Stopwatch.GetTimestamp();
            record.StepTimestamp = began;
            return new NexFlowScope(record, began);
        }

        internal static void Complete(NexFlowRecord record, long startTimestamp)
        {
            if (record == null) return;

            record.TotalMs = ElapsedMs(startTimestamp);

            var overheadStart = Stopwatch.GetTimestamp();
            for (int i = 0; i < _sinks.Count; i++)
            {
                // A broken sink must not break the interaction it is observing.
                try { _sinks[i].Emit(record); }
                catch (Exception) { /* sink failures are never fatal to the traced flow */ }
            }

            record.Reset();
            if (_pool.Count < 16) _pool.Push(record);
            _overheadMs += ElapsedMs(overheadStart);
        }

        internal static double ElapsedMs(long fromTimestamp)
            => (Stopwatch.GetTimestamp() - fromTimestamp) * 1000d / Stopwatch.Frequency;
    }

    /// <summary>
    /// The handle an interaction records its hops into. A struct so an untraced interaction
    /// (<c>Level == Off</c>) allocates nothing at all: the scope is empty and every method
    /// returns immediately.
    /// </summary>
    /// <remarks>
    /// Immutable on purpose. The scope is almost always held by a <c>using</c>, whose variable is
    /// readonly - any field this struct mutated would be mutated on a defensive copy and lost.
    /// The per-step cursor therefore lives on the record, which is a reference type.
    /// </remarks>
    public readonly struct NexFlowScope : IDisposable
    {
        private readonly NexFlowRecord _record;
        private readonly long _startTimestamp;

        internal NexFlowScope(NexFlowRecord record, long startTimestamp)
        {
            _record = record;
            _startTimestamp = startTimestamp;
        }

        /// <summary>False for a scope created while tracing was off.</summary>
        public bool IsRecording => _record != null;

        /// <summary>
        /// Records one hop. Duration is measured from the end of the previous step, so the times
        /// add up to the total instead of each step re-measuring from the start.
        /// </summary>
        public void Step(string receiver, string action, NexFlowStatus status = NexFlowStatus.Ok,
            string detail = null, string diagnosticCode = null)
        {
            if (_record == null) return;

            if (status == NexFlowStatus.Skipped && NexFlowTrace.Level < NexFlowLevel.Full) return;
            if (!string.IsNullOrEmpty(detail) && NexFlowTrace.Level < NexFlowLevel.Verbose) detail = null;

            var now = Stopwatch.GetTimestamp();
            _record.Steps.Add(new NexFlowStep
            {
                Sender = _record.Steps.Count == 0
                    ? _record.Origin
                    : _record.Steps[_record.Steps.Count - 1].Receiver,
                Receiver = receiver,
                Action = action,
                Status = status,
                DurationMs = (now - _record.StepTimestamp) * 1000d / Stopwatch.Frequency,
                Detail = detail,
                DiagnosticCode = diagnosticCode
            });
            _record.StepTimestamp = now;
        }

        /// <summary>Shorthand for a failed hop carrying a diagnostic code.</summary>
        public void Failed(string receiver, string action, string diagnosticCode, string detail = null)
            => Step(receiver, action, NexFlowStatus.Failed, detail, diagnosticCode);

        /// <summary>Shorthand for a hop that was reached but not run.</summary>
        public void Skipped(string receiver, string action, string reason = null)
            => Step(receiver, action, NexFlowStatus.Skipped, reason);

        public void Dispose() => NexFlowTrace.Complete(_record, _startTimestamp);
    }
}
