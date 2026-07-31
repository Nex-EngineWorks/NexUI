using System;

namespace emiteat.NexUI.Components
{
    /// <summary>Keyboard modifiers that change what a click on a collection item means.</summary>
    [Flags]
    public enum NXInputModifiers
    {
        None = 0,

        /// <summary>Ctrl/Cmd: toggle this item into the selection.</summary>
        Additive = 1 << 0,

        /// <summary>Shift: extend the selection from the anchor to this item.</summary>
        Range = 1 << 1
    }

    /// <summary>
    /// Where multi-selection modifiers come from, kept as a hook because neither input backend can
    /// be assumed.
    /// </summary>
    /// <remarks>
    /// Calling <c>UnityEngine.Input</c> directly throws on a project configured for the Input System
    /// package alone, and referencing the Input System directly would make it a hard dependency. So
    /// the default reports no modifiers - plain single selection, which always works - and an
    /// integration installs the real probe:
    /// <c>emiteat.NexUI.Integrations.InputSystem</c> does this automatically when that package is
    /// present.
    ///
    /// A project on the legacy input manager can install its own in one line:
    /// <code>
    /// NXInputModifierProbe.Provider = () =>
    ///     (Input.GetKey(KeyCode.LeftControl) ? NXInputModifiers.Additive : 0) |
    ///     (Input.GetKey(KeyCode.LeftShift) ? NXInputModifiers.Range : 0);
    /// </code>
    /// </remarks>
    public static class NXInputModifierProbe
    {
        /// <summary>Reports the modifiers held right now. Null means "no modifiers available".</summary>
        public static Func<NXInputModifiers> Provider;

        public static NXInputModifiers Current => Provider != null ? Provider() : NXInputModifiers.None;

        public static bool IsAdditive => (Current & NXInputModifiers.Additive) != 0;

        public static bool IsRange => (Current & NXInputModifiers.Range) != 0;
    }
}
