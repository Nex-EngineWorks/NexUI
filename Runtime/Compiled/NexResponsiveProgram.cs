using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// One authored responsive rule: when it applies, and what it changes.
    /// </summary>
    /// <remarks>
    /// The condition is carried rather than pre-resolved, unlike almost everything else the
    /// compiler emits. A binding target or an element id can be resolved at compile time because
    /// the answer cannot change afterwards; the screen's resolution can change while the game is
    /// running - a window resize, a Steam Deck docking, a device rotating - so the rule has to be
    /// re-evaluated by the runtime and the compiler's job is only to make that evaluation cheap.
    ///
    /// <see cref="UIInputMode"/> is reused from Abstractions rather than mirrored here. Mirroring
    /// an enum costs a numbering contract that has to be tested (see <c>NexConstraintMode</c>), and
    /// that is only worth paying where the compiled assembly must not depend on the source - which
    /// is not the case for Abstractions, the assembly with no dependencies of its own.
    /// </remarks>
    [Serializable]
    public struct NexResponsiveRule
    {
        public string RuleId;

        /// <summary>Smallest screen this rule applies to, inclusive.</summary>
        public Vector2Int MinResolution;

        /// <summary>Largest screen this rule applies to, inclusive.</summary>
        public Vector2Int MaxResolution;

        /// <summary>Input mode this rule is limited to, when <see cref="ConstrainInputMode"/> is set.</summary>
        public UIInputMode InputMode;

        /// <summary>
        /// Whether <see cref="InputMode"/> is part of the condition at all.
        /// </summary>
        /// <remarks>
        /// A separate flag because <see cref="UIInputMode"/> has no "any" member, and adding one
        /// would put a non-mode in an enum that every other caller switches over exhaustively.
        /// </remarks>
        public bool ConstrainInputMode;

        public int DeltaStart;
        public int DeltaCount;

        /// <summary>True when this rule applies to the given screen.</summary>
        /// <remarks>
        /// Both bounds are inclusive: an author who writes 1280-1920 and 1921-2560 has described
        /// two adjacent bands, and an exclusive upper bound would leave 1920 matching nothing.
        /// </remarks>
        public bool Matches(Vector2Int resolution, UIInputMode inputMode)
        {
            if (ConstrainInputMode && inputMode != InputMode) return false;

            return resolution.x >= MinResolution.x && resolution.y >= MinResolution.y &&
                   resolution.x <= MaxResolution.x && resolution.y <= MaxResolution.y;
        }
    }

    /// <summary>
    /// Every responsive rule on a screen, resolved against its node table.
    /// </summary>
    /// <remarks>
    /// Rules are kept in authored order and <em>all</em> matching rules apply, later over earlier.
    /// Picking only the most specific match would need a specificity metric the authoring model
    /// does not have, and it would break the way designers actually write these: a wide "tablet and
    /// up" rule plus a narrow "and on a gamepad" rule on top of it.
    /// </remarks>
    [Serializable]
    public sealed class NexResponsiveProgram
    {
        public List<NexResponsiveRule> Rules = new List<NexResponsiveRule>();
        public List<NexPropertyDelta> Deltas = new List<NexPropertyDelta>();

        public bool IsEmpty => Rules.Count == 0;

        /// <summary>Indices of every rule that applies, in authored order.</summary>
        public void CollectMatching(Vector2Int resolution, UIInputMode inputMode, List<int> into)
        {
            if (into == null) return;
            into.Clear();

            for (int i = 0; i < Rules.Count; i++)
                if (Rules[i].Matches(resolution, inputMode)) into.Add(i);
        }

        /// <summary>The deltas of one rule, in authored order.</summary>
        public IEnumerable<NexPropertyDelta> DeltasFor(int ruleIndex)
        {
            if (ruleIndex < 0 || ruleIndex >= Rules.Count) yield break;

            var rule = Rules[ruleIndex];
            for (int i = 0; i < rule.DeltaCount; i++)
                yield return Deltas[rule.DeltaStart + i];
        }
    }
}
