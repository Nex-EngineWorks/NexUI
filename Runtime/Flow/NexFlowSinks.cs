using System.Collections.Generic;

namespace emiteat.NexUI.Flow
{
    /// <summary>Writes each finished trace to the Unity console as a single multi-line entry.</summary>
    /// <remarks>
    /// One entry per interaction rather than one per hop: the console collapses and rate-limits
    /// repeated lines, which would hide exactly the repetition a trace is meant to reveal.
    /// </remarks>
    public sealed class NexFlowConsoleSink : INexFlowSink
    {
        public void Emit(NexFlowRecord record)
        {
            if (record == null) return;
            UnityEngine.Debug.Log("[NexUI Flow]\n" + record);
        }
    }

    /// <summary>
    /// Keeps the last N traces in memory for the runtime debugger and for scenario replay
    /// comparison, without touching the console.
    /// </summary>
    /// <remarks>
    /// Records are copied out of the tracer's pool on arrival. The pool reuses record objects, so
    /// holding the instance itself would give a buffer that the next interaction overwrites.
    /// </remarks>
    public sealed class NexFlowMemorySink : INexFlowSink
    {
        private readonly Queue<NexFlowRecord> _records = new Queue<NexFlowRecord>();

        public int Capacity { get; }

        public NexFlowMemorySink(int capacity = 128)
        {
            Capacity = capacity < 1 ? 1 : capacity;
        }

        public IEnumerable<NexFlowRecord> Records => _records;

        public int Count => _records.Count;

        public void Emit(NexFlowRecord record)
        {
            if (record == null) return;

            var copy = new NexFlowRecord
            {
                Origin = record.Origin,
                StartedAt = record.StartedAt,
                TotalMs = record.TotalMs
            };
            copy.Steps.AddRange(record.Steps);

            _records.Enqueue(copy);
            while (_records.Count > Capacity) _records.Dequeue();
        }

        public void Clear() => _records.Clear();
    }
}
