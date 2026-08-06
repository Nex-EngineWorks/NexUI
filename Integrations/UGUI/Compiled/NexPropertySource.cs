using emiteat.NexUI.Compiled;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Where a control's authored settings come from, so one applier serves both writers.
    /// </summary>
    /// <remarks>
    /// Two callers need to turn "input.maxLength = 40" into a call on a uGUI component: the Studio's
    /// prefab writer, reading authoring metadata, and the compiled runtime, reading a
    /// <see cref="NexNodeProgram"/>. Those are different types in different assemblies, and the
    /// only thing they disagree about is where the value is stored.
    ///
    /// Before this, only the prefab writer had the logic. The compiled path silently applied none
    /// of it, which is why a screen could save with a character limit and run without one. Giving
    /// them a shared applier over this interface is what stops the two paths from drifting again -
    /// a property added for one is available to the other by construction.
    ///
    /// Only overridden values are reported. <c>TryGet</c> returning false means "the author left
    /// the default", which is different from "the value happens to equal the default" - the applier
    /// uses that to avoid writing over a control's own defaults.
    /// </remarks>
    public interface INexPropertySource
    {
        bool TryGetFloat(string key, out float value);

        bool TryGetInt(string key, out int value);

        bool TryGetBool(string key, out bool value);

        bool TryGetString(string key, out string value);

        bool TryGetColor(string key, out Color value);

        /// <summary>Enum values travel as their member name, matching how authoring stores them.</summary>
        bool TryGetEnumName(string key, out string value);
    }

    /// <summary>Reads authored settings out of a compiled node.</summary>
    /// <remarks>
    /// A struct with no allocation: the builder makes one per node while wiring, and a screen of
    /// two hundred nodes should not produce two hundred garbage objects to read four settings.
    /// </remarks>
    public readonly struct NexProgramPropertySource : INexPropertySource
    {
        private readonly NexNodeProgram _node;

        public NexProgramPropertySource(in NexNodeProgram node) => _node = node;

        public bool TryGetFloat(string key, out float value)
        {
            if (_node.TryGetProperty(key, out var property))
            {
                value = property.Number;
                return true;
            }
            value = 0f;
            return false;
        }

        public bool TryGetInt(string key, out int value)
        {
            if (_node.TryGetProperty(key, out var property))
            {
                value = property.AsInt();
                return true;
            }
            value = 0;
            return false;
        }

        public bool TryGetBool(string key, out bool value)
        {
            if (_node.TryGetProperty(key, out var property))
            {
                // Authoring may have stored a flag as a number, and a compiled asset written by an
                // older Studio can carry either. Accepting both beats refusing a value that is there.
                value = property.Kind == NexPropertyKind.Flag ? property.Flag : property.Number != 0f;
                return true;
            }
            value = false;
            return false;
        }

        public bool TryGetString(string key, out string value)
        {
            if (_node.TryGetProperty(key, out var property))
            {
                value = property.Text ?? string.Empty;
                return true;
            }
            value = string.Empty;
            return false;
        }

        public bool TryGetColor(string key, out Color value)
        {
            if (_node.TryGetProperty(key, out var property))
            {
                value = property.Color;
                return true;
            }
            value = Color.white;
            return false;
        }

        public bool TryGetEnumName(string key, out string value) => TryGetString(key, out value);
    }
}
