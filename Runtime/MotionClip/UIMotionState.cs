namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// The fixed component-state set a <see cref="UIMotionStateMachine"/> can transition between
    /// (brief §10). Deliberately a plain Runtime enum, independent of the Designer's Editor-only
    /// <c>DesignerComponentState</c> flags enum - a shipped game executes state transitions without
    /// the Editor assembly present, so this can't depend on it.
    /// </summary>
    public enum UIMotionState
    {
        Normal = 0,
        Hover = 1,
        Pressed = 2,
        Focused = 3,
        Selected = 4,
        Disabled = 5,
        Loading = 6,
        Error = 7,
        Success = 8
    }
}
