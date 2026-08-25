using System;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// Reads and writes a built control's value without the caller knowing which control it is.
    /// </summary>
    /// <remarks>
    /// One handle for slider, scrollbar, toggle and dropdown is what keeps the binding code to a
    /// single path. The compiled program already says "this holds a number" via
    /// <see cref="NexNodeCapabilities.Value"/>; which concrete type provides it is a backend's
    /// problem and nobody else's.
    ///
    /// Declared in the compiled assembly rather than in either backend, because both backends need
    /// the same vocabulary and neither may reference the other. Two identical interfaces, one per
    /// backend, would have meant the binding code in each was written against a different type that
    /// only looked the same.
    /// </remarks>
    public interface INexValueHandle
    {
        float Value { get; set; }

        /// <summary>Raised when the user changed it - never when a binding wrote it.</summary>
        event Action<float> UserChanged;

        void Dispose();
    }

    /// <summary>
    /// Reads and writes a control whose value is text rather than a number.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="INexValueHandle"/> rather than widening it. An input field's value
    /// is a string, and squeezing it through a float would lose it entirely - the two are different
    /// kinds of binding, not two shapes of one.
    ///
    /// This is what makes a text binding two-way. Without it a bound input field could be filled
    /// from state but whatever the user typed went nowhere, which is the half that makes the
    /// control worth having.
    /// </remarks>
    public interface INexTextHandle
    {
        string Text { get; set; }

        /// <summary>Raised when the user changed it - never when a binding wrote it.</summary>
        event Action<string> UserChanged;

        void Dispose();
    }
}
