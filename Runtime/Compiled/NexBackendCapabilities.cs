using System.Collections.Generic;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// A backend a compiled screen can be built by.
    /// </summary>
    /// <remarks>
    /// An enum rather than a type reference, because the thing that needs naming is the
    /// <em>capability profile</em>, not the builder class. That is what lets Studio report on a
    /// backend the editor is not currently able to instantiate, and it is the extension point the
    /// independent renderer would slot into: a new member plus a row in the table below, with no
    /// branch anywhere else.
    ///
    /// Appended, never renumbered - a compile report keyed to these values outlives the session
    /// that produced it.
    /// </remarks>
    public enum NexBackendId
    {
        UGui = 0,
        UIToolkit = 1
    }

    /// <summary>
    /// One thing a screen can ask for that a backend either does or does not do.
    /// </summary>
    /// <remarks>
    /// Granular enough to be actionable and no finer. "Corner radius" is here because an author
    /// can remove it; "border colour" is not, because a backend that draws borders draws them in
    /// colour. The test is whether a row would change what somebody does.
    ///
    /// These are capabilities, not fields. Several fields map to one capability where a backend
    /// supports them as a unit - a drop shadow's colour, offset and blur stand or fall together.
    ///
    /// Appended, never renumbered.
    /// </remarks>
    public enum NexCapability
    {
        LayoutWrap = 0,
        LayoutMaxSize = 1,
        LayoutAspectRatio = 2,
        LayoutGrid = 3,

        /// <summary>Space-between / space-around distribution along the main axis.</summary>
        LayoutSpaceDistribution = 4,

        LayoutMargin = 5,

        AppearanceCornerRadius = 6,

        /// <summary>An inset border, as opposed to an outline drawn outside the box.</summary>
        AppearanceBorder = 7,

        /// <summary>An outline drawn outside the element's box.</summary>
        AppearanceOutline = 8,

        AppearanceDropShadow = 9,
        AppearanceShadowBlur = 10,
        AppearanceInnerShadow = 11,
        AppearanceBackgroundBlur = 12,
        AppearanceCrop = 13,

        TypographyAutoSize = 14,
        TypographyEllipsis = 15,
        TypographyLineHeight = 16,
        TypographyTextShadow = 17,
        TypographyTextOutline = 18,
        TypographyRightToLeft = 19,
        TypographyFontWeight = 20,

        /// <summary>A cascading class list on an element.</summary>
        StyleClasses = 21,

        /// <summary>Per-element theme token overrides resolved at runtime.</summary>
        ThemeTokens = 22,

        /// <summary>Playing an authored motion clip.</summary>
        Motion = 23,

        /// <summary>Resolving a localization key against a table.</summary>
        Localization = 24
    }

    /// <summary>
    /// What each backend can do, as data rather than as branches inside each backend.
    /// </summary>
    /// <remarks>
    /// One table, read by three consumers: the compile report that tells an author what a screen
    /// will lose before it ships, the runtime appliers that decide whether to report a gap while
    /// building, and any tooling that wants to compare backends. Consumers that decide the same
    /// question from different code will eventually disagree, and "the report said it was fine"
    /// against "the screen came out wrong" is the worst version of that.
    ///
    /// The two current backends are <em>not</em> ordered by capability. UI Toolkit does natively
    /// what uGUI can only approximate - wrapping, maximum size, corner radius, space distribution,
    /// text outline, style classes - and cannot do things uGUI can: box shadow, auto-sizing text,
    /// ellipsis, line height, aspect ratio, a fixed-cell grid. Neither is a subset of the other,
    /// which is why the compiler carries everything and this table is a matrix rather than a rank.
    /// </remarks>
    public static class NexBackendCapabilities
    {
        /// <summary>Capabilities a backend does <em>not</em> have. Absence is the common case.</summary>
        /// <remarks>
        /// Stored as the exception list rather than the support list so that adding a capability
        /// defaults every backend to "supported" only where someone has said otherwise - and so a
        /// new backend starts by declaring what it cannot do, which is the shorter and more
        /// honest list to write.
        /// </remarks>
        private static readonly Dictionary<NexBackendId, HashSet<NexCapability>> Missing =
            new Dictionary<NexBackendId, HashSet<NexCapability>>
            {
                [NexBackendId.UGui] = new HashSet<NexCapability>
                {
                    // Layout groups do not wrap, LayoutElement has no maximum, and childAlignment
                    // cannot distribute space; AspectRatioFitter competes with a layout group
                    // rather than composing with it.
                    NexCapability.LayoutWrap,
                    NexCapability.LayoutMaxSize,
                    NexCapability.LayoutAspectRatio,
                    NexCapability.LayoutSpaceDistribution,
                    NexCapability.LayoutMargin,

                    // Stock uGUI has no rounded-rect renderer and no inset border; Outline draws
                    // outside the rect, and Shadow is a hard offset copy rather than a blur.
                    NexCapability.AppearanceCornerRadius,
                    NexCapability.AppearanceBorder,
                    NexCapability.AppearanceShadowBlur,
                    NexCapability.AppearanceInnerShadow,
                    NexCapability.AppearanceBackgroundBlur,
                    NexCapability.AppearanceCrop,

                    // TMP resolves weight through a font asset's weight table, and the compiled
                    // program deliberately carries no font assets.
                    NexCapability.TypographyFontWeight,

                    NexCapability.StyleClasses,
                    NexCapability.ThemeTokens,
                    NexCapability.Motion,
                    NexCapability.Localization
                },

                [NexBackendId.UIToolkit] = new HashSet<NexCapability>
                {
                    NexCapability.LayoutAspectRatio,
                    NexCapability.LayoutGrid,

                    // No box shadow of any kind, and nothing that can sample what is behind an
                    // element.
                    NexCapability.AppearanceOutline,
                    NexCapability.AppearanceDropShadow,
                    NexCapability.AppearanceShadowBlur,
                    NexCapability.AppearanceInnerShadow,
                    NexCapability.AppearanceBackgroundBlur,

                    NexCapability.TypographyAutoSize,
                    NexCapability.TypographyEllipsis,
                    NexCapability.TypographyLineHeight,
                    NexCapability.TypographyTextShadow,
                    NexCapability.TypographyRightToLeft,
                    NexCapability.TypographyFontWeight,

                    NexCapability.ThemeTokens,
                    NexCapability.Motion,
                    NexCapability.Localization
                }
            };

        /// <summary>True when a backend can honour a capability as authored.</summary>
        public static bool Supports(NexBackendId backend, NexCapability capability)
            => !Missing.TryGetValue(backend, out var missing) || !missing.Contains(capability);

        /// <summary>Everything a backend cannot honour, for a report that lists them up front.</summary>
        public static IEnumerable<NexCapability> MissingFrom(NexBackendId backend)
            => Missing.TryGetValue(backend, out var missing)
                ? (IEnumerable<NexCapability>)missing
                : System.Array.Empty<NexCapability>();

        /// <summary>Every backend the table describes.</summary>
        public static IEnumerable<NexBackendId> Backends => Missing.Keys;

        /// <summary>A short author-facing name. The enum name is for code, not for a report.</summary>
        public static string DisplayName(NexCapability capability)
        {
            switch (capability)
            {
                case NexCapability.LayoutWrap: return "Wrap";
                case NexCapability.LayoutMaxSize: return "Maximum size";
                case NexCapability.LayoutAspectRatio: return "Aspect ratio";
                case NexCapability.LayoutGrid: return "Grid layout";
                case NexCapability.LayoutSpaceDistribution: return "Space distribution";
                case NexCapability.LayoutMargin: return "Margin";
                case NexCapability.AppearanceCornerRadius: return "Corner radius";
                case NexCapability.AppearanceBorder: return "Border";
                case NexCapability.AppearanceOutline: return "Outline";
                case NexCapability.AppearanceDropShadow: return "Drop shadow";
                case NexCapability.AppearanceShadowBlur: return "Shadow blur";
                case NexCapability.AppearanceInnerShadow: return "Inner shadow";
                case NexCapability.AppearanceBackgroundBlur: return "Background blur";
                case NexCapability.AppearanceCrop: return "Crop";
                case NexCapability.TypographyAutoSize: return "Auto font size";
                case NexCapability.TypographyEllipsis: return "Ellipsis";
                case NexCapability.TypographyLineHeight: return "Line height";
                case NexCapability.TypographyTextShadow: return "Text shadow";
                case NexCapability.TypographyTextOutline: return "Text outline";
                case NexCapability.TypographyRightToLeft: return "Right to left";
                case NexCapability.TypographyFontWeight: return "Font weight";
                case NexCapability.StyleClasses: return "Style classes";
                case NexCapability.ThemeTokens: return "Theme token overrides";
                case NexCapability.Motion: return "Motion clips";
                case NexCapability.Localization: return "Localization lookup";
                default: return capability.ToString();
            }
        }

        public static string DisplayName(NexBackendId backend)
            => backend == NexBackendId.UIToolkit ? "UI Toolkit" : "uGUI";
    }
}
