using System;
using System.Globalization;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// Compares a live value against an authored one, the same way everywhere.
    /// </summary>
    /// <remarks>
    /// Shared because two callers ask exactly this question - an interaction condition
    /// ("only if Player.Level &gt; 5") and a scenario assertion ("assert Currency == 500") - and
    /// if they answered it differently, a rule that fires in the game would fail its own test for
    /// reasons nobody could see.
    ///
    /// Three comparisons, tried in order:
    ///
    /// <list type="number">
    /// <item><b>Boolean</b>, when the live value is a bool and the authored text reads as one.
    /// This case exists because the obvious fallback gets it wrong: <c>true.ToString()</c> is
    /// <c>"True"</c> in .NET, so an authored <c>"true"</c> would fail a plain text comparison.</item>
    /// <item><b>Numeric</b>, when both sides are numbers.</item>
    /// <item><b>Text</b>, otherwise.</item>
    /// </list>
    ///
    /// Ordering comparisons on non-numeric values return false rather than falling back to
    /// lexicographic order. "Greater than" between two strings is almost always an authoring
    /// mistake, and answering it silently would hide that mistake as a rule that never fires.
    /// </remarks>
    public static class NexValueComparison
    {
        /// <summary>Compares against authored text, parsing it as needed.</summary>
        public static bool Matches(object live, NexComparison comparison, string expectedText)
        {
            var isNumeric = TryParseNumber(expectedText, out var expectedNumber);
            return Matches(live, comparison, expectedText, expectedNumber, isNumeric);
        }

        /// <summary>
        /// Compares against authored text whose numeric form was already parsed - the compiled
        /// path, where parsing happened once at compile time instead of on every evaluation.
        /// </summary>
        public static bool Matches(object live, NexComparison comparison, string expectedText,
            double expectedNumber, bool expectedIsNumeric)
        {
            if (live is bool liveBool && TryParseBool(expectedText, out var expectedBool))
            {
                switch (comparison)
                {
                    case NexComparison.Equals: return liveBool == expectedBool;
                    case NexComparison.NotEquals: return liveBool != expectedBool;
                    default: return false;
                }
            }

            if (expectedIsNumeric && TryToNumber(live, out var liveNumber))
            {
                switch (comparison)
                {
                    case NexComparison.Equals: return Math.Abs(liveNumber - expectedNumber) < 0.000001d;
                    case NexComparison.NotEquals: return Math.Abs(liveNumber - expectedNumber) >= 0.000001d;
                    case NexComparison.GreaterThan: return liveNumber > expectedNumber;
                    case NexComparison.LessThan: return liveNumber < expectedNumber;
                }
            }

            var liveText = Describe(live);
            var expected = expectedText ?? string.Empty;

            switch (comparison)
            {
                case NexComparison.Equals: return string.Equals(liveText, expected, StringComparison.Ordinal);
                case NexComparison.NotEquals: return !string.Equals(liveText, expected, StringComparison.Ordinal);
                default: return false;
            }
        }

        /// <summary>Renders a live value for a diagnostic or a trace line.</summary>
        public static string Describe(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case float f: return f.ToString(CultureInfo.InvariantCulture);
                case double d: return d.ToString(CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }

        public static bool TryParseNumber(string text, out double number)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
               && !string.IsNullOrEmpty(text);

        private static bool TryParseBool(string text, out bool value)
            => bool.TryParse(text, out value);

        private static bool TryToNumber(object value, out double number)
        {
            switch (value)
            {
                case null: number = 0d; return false;
                case double d: number = d; return true;
                case float f: number = f; return true;
                case int i: number = i; return true;
                case long l: number = l; return true;
                case bool b: number = b ? 1d : 0d; return true;
                case string s: return TryParseNumber(s, out number);
                default:
                    number = 0d;
                    return false;
            }
        }
    }
}
