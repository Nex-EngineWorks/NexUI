using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Theme
{
    /// <summary>
    /// A responsive breakpoint rule: when the reference width is at least
    /// <see cref="minWidth"/>, its token overrides apply. Rules are evaluated in
    /// ascending width order so the widest matching rule wins.
    /// </summary>
    [Serializable]
    public sealed class ResponsiveRule
    {
        public string name;
        public float minWidth;
        public ThemeToken[] overrides = Array.Empty<ThemeToken>();

        public bool Matches(float width) => width >= minWidth;
    }

    /// <summary>Evaluates a set of responsive rules against a width and applies matches.</summary>
    public sealed class ResponsiveRuleSet
    {
        private readonly List<ResponsiveRule> _rules = new List<ResponsiveRule>();

        public void Add(ResponsiveRule rule)
        {
            if (rule == null) return;
            _rules.Add(rule);
            _rules.Sort((a, b) => a.minWidth.CompareTo(b.minWidth));
        }

        /// <summary>Apply all matching rules (ascending) into the override layer.</summary>
        public void Apply(float width, RuntimeTokenOverride target)
        {
            if (target == null) return;
            foreach (var rule in _rules)
            {
                if (!rule.Matches(width)) continue;
                if (rule.overrides == null) continue;
                foreach (var t in rule.overrides)
                    if (t != null && !string.IsNullOrEmpty(t.key))
                        target.Set(t.key, t.value);
            }
        }
    }
}
