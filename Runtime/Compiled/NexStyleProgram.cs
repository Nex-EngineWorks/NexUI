using System;

namespace emiteat.NexUI.Compiled
{
    /// <summary>One theme token overridden on a single node.</summary>
    [Serializable]
    public struct NexTokenOverride
    {
        public string Key;
        public string Value;
    }

    /// <summary>
    /// The style identity of a compiled node: the classes it carries, the theme it asks for, and
    /// the tokens it overrides locally.
    /// </summary>
    /// <remarks>
    /// Style classes were authorable and were dropped by the compiler, like layout, appearance,
    /// typography, fragments and Blocks before them. The node already carried a
    /// <see cref="NexNodeProgram.ClassBindingKey"/> - the key that *changes* a class at runtime -
    /// with no way to say what the class was to begin with, which made the binding the only way to
    /// have one.
    ///
    /// Token overrides are per node on purpose. A theme is a screen-level choice; overriding one
    /// token on one element ("this slot's accent is the rarity colour") is an element-level one,
    /// and forcing it up to the theme would mean a theme per rarity.
    /// </remarks>
    [Serializable]
    public struct NexStyleProgram
    {
        /// <summary>Static style classes, in authored order. Never null after lowering.</summary>
        public string[] Classes;

        /// <summary>Theme this node asks for, or empty to inherit the screen's.</summary>
        public string ThemeId;

        public NexTokenOverride[] TokenOverrides;

        public bool IsEmpty =>
            (Classes == null || Classes.Length == 0) &&
            string.IsNullOrEmpty(ThemeId) &&
            (TokenOverrides == null || TokenOverrides.Length == 0);
    }
}
