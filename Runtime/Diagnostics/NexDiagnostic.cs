using System;
using System.Text;

namespace emiteat.NexUI.Diagnostics
{
    /// <summary>
    /// One structured problem report. Immutable, allocation-cheap to pass around, and safe to
    /// keep after the operation that produced it has finished.
    /// </summary>
    /// <remarks>
    /// Every failure path in the compile / publish pipeline produces one of these instead of a
    /// thrown exception or a <c>Debug.LogWarning</c>, for three reasons: a pass can report many
    /// problems and keep going, the caller decides what is fatal, and the same record can be
    /// rendered into the console, a build report or a JSON bundle without being re-parsed.
    ///
    /// <see cref="Cause"/> forms the cause chain described in the error catalog: the outermost
    /// diagnostic is what the user sees first ("screen failed to compile"), and following
    /// <see cref="Cause"/> leads to the thing that actually went wrong.
    /// </remarks>
    public sealed class NexDiagnostic
    {
        /// <summary>Stable <c>NEX-{SUBSYSTEM}-{NUMBER}</c> identifier. See <see cref="NexDiagnosticCodes"/>.</summary>
        public string Code { get; }

        public NexSeverity Severity { get; }

        /// <summary>One sentence, written for the person authoring the screen.</summary>
        public string Message { get; }

        /// <summary>Optional technical detail: type names, values, pass names.</summary>
        public string Detail { get; }

        /// <summary>What to do about it. Empty when the fix is not mechanical.</summary>
        public string Resolution { get; }

        public NexSourceLocation Location { get; }

        /// <summary>The diagnostic underneath this one, or null when this is the root cause.</summary>
        public NexDiagnostic Cause { get; }

        public NexDiagnostic(
            string code,
            NexSeverity severity,
            string message,
            NexSourceLocation location = default,
            string detail = null,
            string resolution = null,
            NexDiagnostic cause = null)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("Diagnostic code is required.", nameof(code));

            Code = code;
            Severity = severity;
            Message = message ?? string.Empty;
            Location = location;
            Detail = detail ?? string.Empty;
            Resolution = resolution ?? string.Empty;
            Cause = cause;
        }

        /// <summary>Wraps this diagnostic as the cause of a higher-level one.</summary>
        public NexDiagnostic AsCauseOf(string code, NexSeverity severity, string message,
            NexSourceLocation location = default, string resolution = null)
            => new NexDiagnostic(code, severity, message, location, null, resolution, this);

        /// <summary>The deepest diagnostic in the cause chain - the thing to actually fix.</summary>
        public NexDiagnostic RootCause()
        {
            var current = this;
            while (current.Cause != null) current = current.Cause;
            return current;
        }

        /// <summary>Single line, for consoles and list views.</summary>
        public override string ToString()
            => Location.IsNone
                ? Code + ": " + Message
                : Code + ": " + Message + "  (" + Location + ")";

        /// <summary>Multi-line rendering including the full cause chain and the resolution.</summary>
        public string ToDetailedString()
        {
            var sb = new StringBuilder();
            sb.Append(Severity).Append(' ').Append(Code).Append(": ").Append(Message);
            if (!Location.IsNone) sb.Append("\n  at ").Append(Location);
            if (!string.IsNullOrEmpty(Detail)) sb.Append("\n  ").Append(Detail);

            var cause = Cause;
            while (cause != null)
            {
                sb.Append("\n\nCause:\n  ").Append(cause.Code).Append(": ").Append(cause.Message);
                if (!cause.Location.IsNone) sb.Append("\n  at ").Append(cause.Location);
                if (!string.IsNullOrEmpty(cause.Detail)) sb.Append("\n  ").Append(cause.Detail);
                cause = cause.Cause;
            }

            var resolution = RootCause().Resolution;
            if (string.IsNullOrEmpty(resolution)) resolution = Resolution;
            if (!string.IsNullOrEmpty(resolution)) sb.Append("\n\nFix:\n  ").Append(resolution);

            return sb.ToString();
        }
    }
}
