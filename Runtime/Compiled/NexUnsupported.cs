namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// The one question a backend applier asks before reporting a gap.
    /// </summary>
    /// <remarks>
    /// Both halves of that question have exactly one implementation: whether the node asks for the
    /// capability (<see cref="NexCapabilityUse"/>) and whether the backend can do it
    /// (<see cref="NexBackendCapabilities"/>). Before this, each applier decided both inline, and
    /// the compile report decided them again elsewhere - so "the report said the screen was fine"
    /// and "the screen came out wrong" could both be true at once.
    ///
    /// Trivially small on purpose. The value is not in the code, it is in there being one caller
    /// shape: an applier that wants to report a gap cannot do it without going through the table.
    /// </remarks>
    public static class NexUnsupported
    {
        /// <summary>True when this node wants something this backend cannot do.</summary>
        public static bool Applies(in NexNodeProgram node, NexBackendId backend, NexCapability capability)
            => NexCapabilityUse.Uses(node, capability) &&
               !NexBackendCapabilities.Supports(backend, capability);
    }
}
