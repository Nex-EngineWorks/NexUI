using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace emiteat.NexUI.Flow
{
    /// <summary>How much interaction detail is recorded. Ordered; a step is kept when its level is at or below the active one.</summary>
    public enum NexFlowLevel
    {
        /// <summary>Nothing is recorded and nothing is allocated. The shipping default.</summary>
        Off = 0,

        /// <summary>Origin and outcome only - "StartButton click succeeded in 3.8 ms".</summary>
        Summary = 1,

        /// <summary>Every hop from the input to the final handler. What you want when debugging.</summary>
        Standard = 2,

        /// <summary>Adds per-step payload summaries and binding value changes.</summary>
        Verbose = 3,

        /// <summary>Adds steps that were evaluated and skipped, including why.</summary>
        Full = 4
    }

    /// <summary>Outcome of a single hop.</summary>
    public enum NexFlowStatus
    {
        Ok = 0,
        Failed = 1,

        /// <summary>Reached but deliberately not run - a false condition, a cancelled chain.</summary>
        Skipped = 2
    }

    /// <summary>
    /// One hop in an interaction: who handed off to whom, doing what, and how it went.
    /// </summary>
    /// <remarks>
    /// The <c>Sender -&gt; Receiver</c> shape is the whole point of the trace. A flat list of log
    /// lines tells you what happened; naming both ends of every hop tells you where a chain
    /// stopped, which is the question people actually have when a button "does nothing".
    /// </remarks>
    public struct NexFlowStep
    {
        public string Sender;
        public string Receiver;
        public string Action;
        public NexFlowStatus Status;
        public double DurationMs;

        /// <summary>Short payload or result summary. Recorded from <see cref="NexFlowLevel.Verbose"/> up.</summary>
        public string Detail;

        /// <summary>Diagnostic code when <see cref="Status"/> is <see cref="NexFlowStatus.Failed"/>.</summary>
        public string DiagnosticCode;
    }

    /// <summary>
    /// One complete interaction, from the input that started it to the last handler it reached.
    /// </summary>
    public sealed class NexFlowRecord
    {
        /// <summary>Authoring path of the element the interaction started at, e.g. <c>MainMenu/StartButton</c>.</summary>
        public string Origin;

        public DateTime StartedAt;
        public double TotalMs;
        public readonly List<NexFlowStep> Steps = new List<NexFlowStep>();

        /// <summary>
        /// Timestamp the next step measures from. Lives here, not on the scope struct, because a
        /// <c>using</c> variable is readonly - mutating it from <c>Step</c> would silently act on a
        /// defensive copy and every step would report its duration from the start of the trace.
        /// </summary>
        internal long StepTimestamp;

        /// <summary>False when any step failed.</summary>
        public bool Succeeded
        {
            get
            {
                for (int i = 0; i < Steps.Count; i++)
                    if (Steps[i].Status == NexFlowStatus.Failed) return false;
                return true;
            }
        }

        internal void Reset()
        {
            Origin = null;
            StartedAt = default;
            TotalMs = 0d;
            StepTimestamp = 0L;
            Steps.Clear();
        }

        /// <summary>
        /// Renders the trace in the documented text form. Text rather than a graph because it
        /// pastes into a bug report, diffs between two runs, and works in a player log where no
        /// editor window exists.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(StartedAt.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
              .Append("] ").Append(Origin ?? "<unknown>").Append('\n');

            for (int i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];
                sb.Append("→ ");

                if (!string.IsNullOrEmpty(step.Receiver))
                    sb.Append(step.Receiver).Append('.');
                sb.Append(step.Action);

                switch (step.Status)
                {
                    case NexFlowStatus.Failed:
                        sb.Append("    ✗");
                        if (!string.IsNullOrEmpty(step.DiagnosticCode)) sb.Append(' ').Append(step.DiagnosticCode);
                        break;
                    case NexFlowStatus.Skipped:
                        sb.Append("    - Skipped");
                        break;
                    default:
                        sb.Append("    ✓");
                        break;
                }

                if (!string.IsNullOrEmpty(step.Detail)) sb.Append("  ").Append(step.Detail);
                sb.Append('\n');
            }

            sb.Append(Succeeded ? "✓ " : "✗ ")
              .Append(TotalMs.ToString("F2", CultureInfo.InvariantCulture)).Append(" ms");
            return sb.ToString();
        }
    }

    /// <summary>Receives finished traces. Implement to route them somewhere other than the console.</summary>
    public interface INexFlowSink
    {
        void Emit(NexFlowRecord record);
    }
}
