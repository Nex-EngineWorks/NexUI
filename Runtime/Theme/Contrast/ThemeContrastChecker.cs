using UnityEngine;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// WCAG-style contrast math for theme color pairs. Backend-independent (operates on
    /// <see cref="Color"/>), so it is reusable from tests, runtime and the Designer
    /// contrast panel. Ratios range 1:1 (identical) to 21:1 (black on white).
    /// </summary>
    public static class ThemeContrastChecker
    {
        /// <summary>WCAG AA minimum for normal-size body text.</summary>
        public const float AaNormalText = 4.5f;

        /// <summary>WCAG AA minimum for large text (>= 18pt / 14pt bold).</summary>
        public const float AaLargeText = 3.0f;

        /// <summary>Relative luminance of a color per WCAG 2.x (sRGB, alpha ignored).</summary>
        public static float RelativeLuminance(Color c)
        {
            float r = Linearize(c.r);
            float g = Linearize(c.g);
            float b = Linearize(c.b);
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        /// <summary>Contrast ratio between two colors, always &gt;= 1.</summary>
        public static float ContrastRatio(Color foreground, Color background)
        {
            float l1 = RelativeLuminance(foreground);
            float l2 = RelativeLuminance(background);
            float lighter = Mathf.Max(l1, l2);
            float darker = Mathf.Min(l1, l2);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        /// <summary>True when the pair meets the given minimum ratio (default AA normal).</summary>
        public static bool MeetsAa(Color foreground, Color background, float minimum = AaNormalText)
            => ContrastRatio(foreground, background) >= minimum;

        private static float Linearize(float channel)
            => channel <= 0.03928f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }
}
