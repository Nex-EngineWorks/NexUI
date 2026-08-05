using System;

namespace emiteat.NexUI.Diagnostics
{
    /// <summary>
    /// Which feature a diagnostic came out of, who raised it, and who was handling it.
    /// </summary>
    /// <remarks>
    /// The error code already says what went wrong and the location says where in the document.
    /// Neither answers the question people actually ask first, which is <em>what was I doing</em> -
    /// "Figma Import" and "uGUI Save" can both raise the same binding error about the same element,
    /// and the fix is completely different.
    ///
    /// <see cref="Origin"/> and <see cref="Handler"/> use the same sender/receiver vocabulary as
    /// the flow trace on purpose. A trace line reading <c>FigmaDocumentImporter -> BindingWriter</c>
    /// and a diagnostic attributed to the same pair are describing one event, and giving them two
    /// vocabularies would mean joining them by eye.
    ///
    /// All three are plain strings rather than an enum. The set of features is open - an extension
    /// raises diagnostics too - and an enum would either need editing for every new one or would
    /// push extensions into an "Other" bucket.
    /// </remarks>
    [Serializable]
    public struct NexDiagnosticContext : IEquatable<NexDiagnosticContext>
    {
        public static readonly NexDiagnosticContext None = default;

        /// <summary>
        /// User-facing feature, in the words the feature is called by: "Figma Import", "uGUI Save".
        /// </summary>
        /// <remarks>
        /// Not the subsystem segment of the error code. <c>BND</c> is where the check lives;
        /// the feature is what the user pressed.
        /// </remarks>
        public string Feature;

        /// <summary>What raised it - the pass, importer or writer that found the problem.</summary>
        public string Origin;

        /// <summary>What was being acted on or written to when it was raised. Often empty.</summary>
        public string Handler;

        /// <summary>
        /// Groups every diagnostic produced by one user action.
        /// </summary>
        /// <remarks>
        /// One save can raise a dozen diagnostics across three features. Without this the console
        /// can only sort them by time, and two saves a second apart interleave.
        /// </remarks>
        public string OperationId;

        public NexDiagnosticContext(string feature, string origin = null, string handler = null,
            string operationId = null)
        {
            Feature = feature ?? string.Empty;
            Origin = origin ?? string.Empty;
            Handler = handler ?? string.Empty;
            OperationId = operationId ?? string.Empty;
        }

        public bool IsNone =>
            string.IsNullOrEmpty(Feature) &&
            string.IsNullOrEmpty(Origin) &&
            string.IsNullOrEmpty(Handler) &&
            string.IsNullOrEmpty(OperationId);

        /// <summary>
        /// This context laid over <paramref name="outer"/>: anything unset here is inherited.
        /// </summary>
        /// <remarks>
        /// What makes nesting worth having. An inner scope that only names a handler still reports
        /// the feature the outer scope established, so a writer deep inside a save does not have to
        /// know - or repeat - that it is part of "uGUI Save".
        /// </remarks>
        public NexDiagnosticContext InheritingFrom(NexDiagnosticContext outer)
            => new NexDiagnosticContext(
                string.IsNullOrEmpty(Feature) ? outer.Feature : Feature,
                string.IsNullOrEmpty(Origin) ? outer.Origin : Origin,
                string.IsNullOrEmpty(Handler) ? outer.Handler : Handler,
                string.IsNullOrEmpty(OperationId) ? outer.OperationId : OperationId);

        /// <summary>"Origin -> Handler", or just the one that is set.</summary>
        public string Route()
        {
            var hasOrigin = !string.IsNullOrEmpty(Origin);
            var hasHandler = !string.IsNullOrEmpty(Handler);

            if (hasOrigin && hasHandler) return Origin + " -> " + Handler;
            if (hasOrigin) return Origin;
            return hasHandler ? "-> " + Handler : string.Empty;
        }

        public override string ToString()
        {
            if (IsNone) return string.Empty;

            var route = Route();
            if (string.IsNullOrEmpty(Feature)) return route;
            return string.IsNullOrEmpty(route) ? Feature : Feature + ": " + route;
        }

        public bool Equals(NexDiagnosticContext other)
            => Feature == other.Feature && Origin == other.Origin
               && Handler == other.Handler && OperationId == other.OperationId;

        public override bool Equals(object obj) => obj is NexDiagnosticContext other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Feature != null ? Feature.GetHashCode() : 0;
                hash = (hash * 397) ^ (Origin != null ? Origin.GetHashCode() : 0);
                hash = (hash * 397) ^ (Handler != null ? Handler.GetHashCode() : 0);
                hash = (hash * 397) ^ (OperationId != null ? OperationId.GetHashCode() : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// The feature names NexUI itself reports under.
    /// </summary>
    /// <remarks>
    /// Constants rather than free strings so a rename lands everywhere and the console's grouping
    /// does not split into "uGUI Save" and "UGUI Save". English throughout, matching the diagnostic
    /// messages and the error catalog - these end up in bug reports that get searched.
    ///
    /// Third-party features supply their own strings; nothing here is a closed set.
    /// </remarks>
    public static class NexDiagnosticFeatures
    {
        public const string Compile = "Compile";
        public const string Publish = "Publish";
        public const string Validation = "Validation";
        public const string UGuiSave = "uGUI Save";
        public const string UIToolkitSave = "UI Toolkit Save";
        public const string FigmaImport = "Figma Import";
        public const string Migration = "Migration";
        public const string Runtime = "Runtime";
        public const string Interaction = "Interaction";
        public const string Accessibility = "Accessibility";
        public const string Motion = "Motion";
        public const string Scenario = "Scenario";
        public const string Components = "Components";
    }
}
