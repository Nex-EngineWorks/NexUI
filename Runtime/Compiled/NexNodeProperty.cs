using System;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// One authored control property, carried into the compiled program by key.
    /// </summary>
    /// <remarks>
    /// The alternative was a field on <see cref="NexNodeProgram"/> per property - maxLength,
    /// readOnly, contentType, lineType, seven scroll settings, and so on for every control the
    /// backend learns to build. That is a struct that grows without bound, is mostly empty on
    /// every node, and forces a compiled-asset migration for each addition.
    ///
    /// A keyed bag matches how the authoring model already stores these: only what the user
    /// changed is written, and a key this build does not recognise survives a round trip instead
    /// of being dropped. The runtime applies the keys it knows and ignores the rest, so a screen
    /// authored in a newer Studio still loads in an older player - it just does less.
    ///
    /// Keys are the authoring schema keys ("input.maxLength", "scroll.inertia"), so one vocabulary
    /// covers authoring, the prefab writer and the compiled runtime. Three names for one setting
    /// is how they drift.
    /// </remarks>
    [Serializable]
    public struct NexNodeProperty
    {
        /// <summary>Authoring schema key, e.g. <c>input.maxLength</c>.</summary>
        public string Key;

        public NexPropertyKind Kind;

        public float Number;

        public bool Flag;

        public string Text;

        public Color Color;

        public Vector2 Vector;

        public static NexNodeProperty OfNumber(string key, float value)
            => new NexNodeProperty { Key = key, Kind = NexPropertyKind.Number, Number = value };

        public static NexNodeProperty OfFlag(string key, bool value)
            => new NexNodeProperty { Key = key, Kind = NexPropertyKind.Flag, Flag = value };

        public static NexNodeProperty OfText(string key, string value)
            => new NexNodeProperty { Key = key, Kind = NexPropertyKind.Text, Text = value ?? string.Empty };

        public static NexNodeProperty OfColor(string key, Color value)
            => new NexNodeProperty { Key = key, Kind = NexPropertyKind.Color, Color = value };

        public static NexNodeProperty OfVector(string key, Vector2 value)
            => new NexNodeProperty { Key = key, Kind = NexPropertyKind.Vector, Vector = value };

        /// <summary>Reads as an int, for the many properties that are counts or enum indices.</summary>
        public int AsInt() => Mathf.RoundToInt(Number);

        public override string ToString() => Key + "=" + ValueText();

        private string ValueText()
        {
            switch (Kind)
            {
                case NexPropertyKind.Flag: return Flag ? "true" : "false";
                case NexPropertyKind.Text: return Text ?? string.Empty;
                case NexPropertyKind.Color: return Color.ToString();
                case NexPropertyKind.Vector: return Vector.ToString();
                default: return Number.ToString("0.###");
            }
        }
    }

    /// <summary>
    /// Which of <see cref="NexNodeProperty"/>'s fields carries the value.
    /// </summary>
    /// <remarks>
    /// Enums and counts both land in <see cref="Number"/> rather than getting their own kinds:
    /// the runtime already knows what a key means, so a separate kind would only let the two
    /// disagree.
    /// </remarks>
    public enum NexPropertyKind
    {
        Number = 0,
        Flag = 1,
        Text = 2,
        Color = 3,
        Vector = 4
    }
}
