using System;
using System.Collections.Generic;
using UnityEngine;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Declares how a screen should adapt for a resolution range and/or input mode
    /// (e.g. below 1280x720 use 4 columns; Mobile Portrait hides the side panel).
    /// A rule is considered active when the current resolution falls within
    /// [<see cref="minResolution"/>, <see cref="maxResolution"/>] and, if
    /// <see cref="inputMode"/> is constrained, the current input mode matches.
    /// </summary>
    [Serializable]
    public sealed class UIResponsiveRule
    {
        public string ruleId;
        public Vector2Int minResolution;
        public Vector2Int maxResolution;
        public UIInputMode inputMode;
        public bool constrainInputMode;
        public List<UIResponsiveOverride> overrides = new();
    }

    /// <summary>A single element property override applied by a responsive rule.</summary>
    [Serializable]
    public sealed class UIResponsiveOverride
    {
        public string elementId;
        public string propertyPath;
        public string value;
    }
}
