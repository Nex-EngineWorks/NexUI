using System;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// One property one node changes, relative to the base screen.
    /// </summary>
    /// <remarks>
    /// Shared by every table that says "under this condition, these nodes differ" - states today,
    /// responsive rules alongside them. They were going to be two identical structs, and a struct
    /// that is copied is a struct that drifts: the day one of them learns a new value kind, the
    /// other silently keeps dropping it.
    ///
    /// A delta, not a snapshot. The authoring model already writes these as differences from the
    /// default, and expanding them into full node copies would multiply a screen's node table by
    /// its state count while re-stating every value the state never touches - so a later edit to a
    /// shared value would have to be propagated into every state to stay correct.
    ///
    /// <see cref="NodeIndex"/> is an index rather than an element id for the reason
    /// <see cref="NexInteractionAction.TargetNodeIndex"/> is: the compiler still had the document
    /// when it resolved the name, so a delta that points at a deleted element is a compile error
    /// instead of a lookup that silently finds nothing on a player device.
    ///
    /// The value is a <see cref="NexNodeProperty"/>, keyed by the authoring property path
    /// (<c>text</c>, <c>tint</c>, <c>runtimeVisible</c>). Reusing that type keeps one value
    /// vocabulary across authoring, the prefab writer and the compiled runtime, and gives these
    /// tables the same forward compatibility: a key an older player does not recognise is skipped
    /// rather than making the whole program unreadable.
    /// </remarks>
    [Serializable]
    public struct NexPropertyDelta
    {
        /// <summary>Node this delta applies to. Always a valid index - the compiler dropped the rest.</summary>
        public int NodeIndex;

        /// <summary>The changed property, keyed by authoring path.</summary>
        public NexNodeProperty Value;
    }
}
