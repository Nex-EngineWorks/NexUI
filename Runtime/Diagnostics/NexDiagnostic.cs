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

        /// <summary>
        /// Which feature raised it, and along which sender/receiver route.
        /// </summary>
        /// <remarks>
        /// Stamped by <see cref="NexDiagnosticBag"/> from the enclosing scope rather than passed at
        /// every call site, so a pass reports problems without also having to know which user
        /// action it is part of.
        /// </remarks>
        public NexDiagnosticContext Context { get; }

        public NexDiagnostic(
            string code,
            NexSeverity severity,
            string message,
            NexSourceLocation location = default,
            string detail = null,
            string resolution = null,
            NexDiagnostic cause = null,
            NexDiagnosticContext context = default)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("Diagnostic code is required.", nameof(code));

            Code = code;
            Severity = severity;
            Message = message ?? string.Empty;
            Location = location;
            Detail = detail ?? string.Empty;
            Resolution = resolution ?? string.Empty;
            Cause = cause;
            Context = context;
        }

        /// <summary>A copy carrying <paramref name="context"/>. Used when a bag stamps its scope on.</summary>
        public NexDiagnostic WithContext(NexDiagnosticContext context)
            => context.Equals(Context)
                ? this
                : new NexDiagnostic(Code, Severity, Message, Location, Detail, Resolution, Cause, context);

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
            if (!string.IsNullOrEmpty(Context.Feature)) sb.Append("\n  in ").Append(Context.Feature);

            // The route goes on its own line rather than beside the feature: on a failed save the
            // chain runs three or four hops deep, and folding it into the header hides the end of it.
            var route = Context.Route();
            if (!string.IsNullOrEmpty(route)) sb.Append("\n  raised by ").Append(route);
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
