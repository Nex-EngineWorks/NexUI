namespace emiteat.NexUI.Time
{
    /// <summary>
    /// Where NexUI reads "now" from.
    /// </summary>
    /// <remarks>
    /// Everything that waits - a delayed interaction action, a motion, a scenario step - reads
    /// time through this instead of touching <c>UnityEngine.Time</c>, so the same authored content
    /// behaves correctly in situations the engine clock cannot express:
    ///
    /// <list type="bullet">
    /// <item>A pause menu animating while the game is frozen needs unscaled time.</item>
    /// <item>Scrubbing a motion timeline in the editor needs time the tool sets directly.</item>
    /// <item>A replayed scenario needs the same timings twice, which a wall clock never gives.</item>
    /// </list>
    ///
    /// Seconds as <c>double</c> rather than <c>float</c>: a session running for hours loses
    /// meaningful precision in a 32-bit accumulator, and "the UI gets choppy after a long
    /// session" is a miserable bug to track down.
    /// </remarks>
    public interface INexTimeSource
    {
        /// <summary>Seconds since this source started. Monotonic - it never goes backwards.</summary>
        double Now { get; }
    }

    /// <summary>
    /// A clock the caller advances by hand.
    /// </summary>
    /// <remarks>
    /// The reason the interface exists. A test or a replay drives this and gets identical results
    /// every run, with no frame rate, no real waiting and no flakiness. It is also what a timeline
    /// scrubber sets when the user drags the playhead.
    /// </remarks>
    public sealed class NexManualTime : INexTimeSource
    {
        public double Now { get; private set; }

        public NexManualTime(double start = 0d) => Now = start;

        /// <summary>Moves time forward. Negative steps are ignored so <see cref="Now"/> stays monotonic.</summary>
        public void Advance(double seconds)
        {
            if (seconds > 0d) Now += seconds;
        }

        /// <summary>
        /// Jumps to an absolute time, for a timeline scrub.
        /// </summary>
        /// <remarks>
        /// The one operation allowed to move time backwards, because that is what dragging a
        /// playhead left means. Callers that hold a deadline computed from an older <see cref="Now"/>
        /// must recompute it - which is why waiting code should store a deadline, not an elapsed
        /// total.
        /// </remarks>
        public void SeekTo(double seconds) => Now = seconds;
    }
}
