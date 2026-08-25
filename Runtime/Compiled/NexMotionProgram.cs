using System;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// The motion a compiled node declares: a preset or a clip id, plus the variant to play for
    /// each interaction state.
    /// </summary>
    /// <remarks>
    /// The compiled uGUI and UI Toolkit runtimes resolve this id through the optional runtime
    /// motion registry. Without a registry the data remains carried and the backend reports an
    /// explicit diagnostic instead of silently dropping the authored behavior.
    ///
    /// Dropping it and carrying it are not equally wrong. Carried, the data survives into the
    /// program, changes the content hash, and lets the backend say out loud that it cannot play it;
    /// dropped, an author's hover animation silently does not exist and nothing distinguishes that
    /// from the animation being wrong. Wiring a player into the compiled runtime is then an
    /// addition rather than a re-plumbing.
    ///
    /// Variants are per state rather than a list of rules because that is what the authoring model
    /// offers, and inventing a richer shape here would carry values no inspector can produce.
    /// </remarks>
    [Serializable]
    public struct NexMotionProgram
    {
        /// <summary>
        /// Motion clip / graph / preset id.
        /// </summary>
        /// <remarks>
        /// One id, not an id plus a preset reference. A preset is a <c>ScriptableObject</c>, and the
        /// canonical form excludes asset identity on purpose - so the compiler resolves the preset
        /// down to the id it carries, which is the stable thing the motion registry looks up anyway.
        /// </remarks>
        public string MotionId;

        public string InitialVariant;
        public string AnimateVariant;
        public string ExitVariant;
        public string HoverVariant;
        public string PressedVariant;
        public string FocusVariant;

        public bool IsEmpty =>
            string.IsNullOrEmpty(MotionId) &&
            string.IsNullOrEmpty(InitialVariant) && string.IsNullOrEmpty(AnimateVariant) &&
            string.IsNullOrEmpty(ExitVariant) && string.IsNullOrEmpty(HoverVariant) &&
            string.IsNullOrEmpty(PressedVariant) && string.IsNullOrEmpty(FocusVariant);
    }
}
