namespace emiteat.NexUI.MotionClip
{
    /// <summary>
    /// UI-facing properties a <see cref="UIMotionClipPropertyTrack"/> can animate. Backed by
    /// <see cref="Abstractions.IUITransformCapability"/> (position/rotation/scale/alpha) and
    /// <see cref="Abstractions.IUISizeCapability"/> (size) on the resolved target element.
    /// </summary>
    public enum UIMotionClipPropertyType
    {
        /// <summary>Anchored position (UGUI RectTransform.anchoredPosition / UI Toolkit translate).</summary>
        AnchoredPosition = 0,

        /// <summary>
        /// Local position. NOTE: the current <see cref="Abstractions.IUITransformCapability"/> does
        /// not distinguish anchored vs. local position — this resolves to the same underlying
        /// value as <see cref="AnchoredPosition"/> until the capability is extended (tracked TODO).
        /// </summary>
        LocalPosition = 1,

        /// <summary>Local rotation around Z, in degrees.</summary>
        LocalRotationZ = 2,

        /// <summary>Local scale (Vector3; UI Toolkit backends currently only honor X/Y).</summary>
        LocalScale = 3,

        /// <summary>Size delta (RectTransform.sizeDelta / UI Toolkit width+height).</summary>
        SizeDelta = 4,

        /// <summary>CanvasGroup alpha / UI Toolkit opacity.</summary>
        CanvasGroupAlpha = 5
    }
}
