namespace emiteat.NexUI.Diagnostics
{
    /// <summary>
    /// How much a diagnostic should interrupt the user. Ordered so that a numeric comparison
    /// works as a threshold filter (<c>severity &gt;= NexSeverity.Warning</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="Error"/> is the line that stops a compile from publishing: anything below it
    /// is reported and the pipeline continues, anything at or above it aborts the publish and
    /// leaves the previously published output untouched.
    /// </remarks>
    public enum NexSeverity
    {
        Trace = 0,
        Debug = 1,
        Information = 2,

        /// <summary>Nothing is wrong, but a better authoring choice exists.</summary>
        Suggestion = 3,

        /// <summary>Compiles and runs, but the result is likely not what the author intended.</summary>
        Warning = 4,

        /// <summary>The affected screen cannot be published.</summary>
        Error = 5,

        /// <summary>The pipeline itself is broken; no further passes are meaningful.</summary>
        Fatal = 6
    }
}
