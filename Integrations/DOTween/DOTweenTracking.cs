#if DOTWEEN
using System.Globalization;
using DG.Tweening;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Flow;
using emiteat.NexUI.Overrides;
using UnityEngine;

namespace emiteat.NexUI.Integrations.DOTween
{
    /// <summary>
    /// Makes DOTween animations a project already has visible to NexUI's debugging layer, without
    /// rewriting them.
    /// </summary>
    /// <remarks>
    /// The other direction already exists: <see cref="DOTweenMotionPlayer"/> plays NexUI motion
    /// through DOTween, which asks a project to move its animations into NexUI first. That is the
    /// wrong order of events for anyone who already has a working DOTween codebase - the tweens
    /// are the part that works, and re-authoring them is a cost paid up front for a benefit that
    /// arrives later, if at all.
    ///
    /// This is the other way round. The tween keeps running exactly as written; NexUI is told what
    /// it is animating, so the override ledger can answer "why is this at 0.4?" with the tween's
    /// name instead of falling silent, and the flow trace shows the animation alongside the
    /// interaction that started it.
    ///
    /// Nothing here changes what a tween does. Every method returns the tween it was handed, so it
    /// drops into an existing chain, and removing the call leaves the animation untouched.
    /// </remarks>
    public static class DOTweenTracking
    {
        /// <summary>
        /// Records this tween as the owner of a node property for as long as it runs.
        /// </summary>
        /// <remarks>
        /// Recorded on start and on completion rather than every frame. A tween writing sixty
        /// records a second would make the ledger a log, and the question it answers - who owns
        /// this value - has the same answer throughout. <paramref name="origin"/> defaults to the
        /// tween's id so an already-named tween needs no extra argument.
        /// </remarks>
        public static Tween TrackAs(this Tween tween, NexOverrideLedger ledger, int nodeIndex,
            NexOverrideProperty property, string origin = null)
        {
            if (tween == null || ledger == null || nodeIndex < 0) return tween;

            var label = Describe(tween, origin);

            tween.OnStart(() => ledger.Record(nodeIndex, property, NexOverrideSource.External,
                "animating", label));

            // On completion the value stops moving, so the ledger records where it landed. A tween
            // that is killed early reports the same way: the last written value is still the answer.
            tween.OnComplete(() => ledger.Record(nodeIndex, property, NexOverrideSource.External,
                "settled", label));
            tween.OnKill(() => ledger.Record(nodeIndex, property, NexOverrideSource.External,
                "stopped", label));

            return tween;
        }

        /// <summary>Same, resolving the node by its authoring id through the screen's source map.</summary>
        public static Tween TrackAs(this Tween tween, NexOverrideLedger ledger,
            NexRuntimeSourceMap sourceMap, string authoringNodeId,
            NexOverrideProperty property, string origin = null)
        {
            if (sourceMap == null || string.IsNullOrEmpty(authoringNodeId)) return tween;

            var instance = sourceMap.InstanceOfNode(authoringNodeId);
            return instance == null
                ? tween
                : tween.TrackAs(ledger, sourceMap.IndexOfInstance(instance), property, origin);
        }

        /// <summary>
        /// Writes the tween's lifetime into the flow trace.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="TrackAs"/> because they answer different questions and cost
        /// different amounts. The ledger is always worth keeping; a trace step per tween is only
        /// worth it while someone is looking, so this does nothing at all when tracing is off
        /// rather than building strings that get discarded.
        /// </remarks>
        public static Tween TraceAs(this Tween tween, string origin = null)
        {
            if (tween == null || !NexFlowTrace.IsEnabled) return tween;

            var label = Describe(tween, origin);

            tween.OnStart(() =>
            {
                using var scope = NexFlowTrace.Begin(label);
                scope.Step("DOTween", "Play");
            });

            tween.OnComplete(() =>
            {
                using var scope = NexFlowTrace.Begin(label);
                scope.Step("DOTween", "Complete", NexFlowStatus.Ok,
                    detail: FormatSeconds(tween.Duration()));
            });

            tween.OnKill(() =>
            {
                using var scope = NexFlowTrace.Begin(label);
                scope.Step("DOTween", "Kill", NexFlowStatus.Skipped);
            });

            return tween;
        }

        /// <summary>Ledger and trace in one call, for the common case.</summary>
        public static Tween TrackAndTrace(this Tween tween, NexOverrideLedger ledger, int nodeIndex,
            NexOverrideProperty property, string origin = null)
            => tween.TrackAs(ledger, nodeIndex, property, origin).TraceAs(origin);

        /// <summary>
        /// A name for the tween: the caller's label, then DOTween's own id, then the target.
        /// </summary>
        /// <remarks>
        /// The fallback chain matters more than it looks. "an external animation changed it" is
        /// true and useless on a screen with a dozen tweens, and the whole point of the ledger is
        /// that the answer names the thing.
        /// </remarks>
        private static string Describe(Tween tween, string origin)
        {
            if (!string.IsNullOrEmpty(origin)) return origin;

            var id = tween.stringId;
            if (!string.IsNullOrEmpty(id)) return "DOTween:" + id;

            return tween.target is Object target && target != null
                ? "DOTween:" + target.name
                : "DOTween:<unnamed>";
        }

        private static string FormatSeconds(float seconds)
            => seconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";
    }
}
#endif
