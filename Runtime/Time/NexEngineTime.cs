namespace emiteat.NexUI.Time
{
    /// <summary>Unity's game clock, affected by <c>Time.timeScale</c>.</summary>
    /// <remarks>
    /// The right default for UI that belongs to the running game - a damage number floating up, a
    /// combo meter draining - because those should slow down and stop when the game does.
    /// </remarks>
    public sealed class NexScaledTime : INexTimeSource
    {
        public double Now => UnityEngine.Time.timeAsDouble;
    }

    /// <summary>Unity's clock ignoring <c>Time.timeScale</c>.</summary>
    /// <remarks>
    /// What menus need. A pause screen that animates in while <c>timeScale</c> is zero would
    /// otherwise never finish its transition, which is the classic version of this bug.
    /// </remarks>
    public sealed class NexUnscaledTime : INexTimeSource
    {
        public double Now => UnityEngine.Time.unscaledTimeAsDouble;
    }

    /// <summary>
    /// The clock NexUI uses when nobody supplied one.
    /// </summary>
    /// <remarks>
    /// Defaults to unscaled, because most NexUI content is menu-like and a frozen menu is a
    /// broken menu, while a HUD running on unscaled time during a pause is merely slightly wrong.
    /// A project that disagrees replaces it once at bootstrap.
    ///
    /// A settable default rather than a hard-coded call is what lets a test swap in
    /// <see cref="NexManualTime"/> without every waiting API growing a parameter.
    /// </remarks>
    public static class NexTime
    {
        private static INexTimeSource _default;

        public static INexTimeSource Default
        {
            get => _default ??= new NexUnscaledTime();
            set => _default = value;
        }

        /// <summary>Restores the built-in unscaled clock. Call from test teardown.</summary>
        public static void ResetDefault() => _default = null;
    }
}
