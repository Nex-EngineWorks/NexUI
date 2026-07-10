namespace emiteat.NexUI.Accessibility
{
    /// <summary>
    /// Semantic role hint for an element, paired with its accessibility label. Mirrors the
    /// common ARIA/UIKit role vocabulary rather than inventing a NexUI-specific one, so it maps
    /// predictably onto whatever native screen-reader/switch-control bridge a backend eventually
    /// exposes for it.
    /// </summary>
    public enum AccessibilityRole
    {
        None = 0,
        Button,
        Label,
        Image,
        Toggle,
        Slider,
        ProgressIndicator,
        List,
        ListItem,
        Container,
        Header,
        Dialog,
        TextField
    }
}
