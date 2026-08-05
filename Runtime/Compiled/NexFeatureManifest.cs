using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// Stable identifiers for the runtime features a compiled screen can require.
    /// </summary>
    /// <remarks>
    /// These are what build stripping keys off: a feature that appears in no published screen's
    /// manifest is a feature whose runtime code and assets do not have to ship. Keeping them as
    /// strings rather than an enum means an extension can declare its own feature without
    /// editing this file.
    /// </remarks>
    public static class NexFeatures
    {
        public const string Text = "nexui.text";
        public const string Image = "nexui.image";
        public const string Button = "nexui.button";
        public const string TextBinding = "nexui.binding.text";
        public const string CommandBinding = "nexui.binding.command";

        /// <summary>The trigger / condition / action engine. Absent screens never load it.</summary>
        public const string Interaction = "nexui.interaction";
    }

    /// <summary>
    /// Which runtime features a compiled screen needs, and why each one is in.
    /// </summary>
    /// <remarks>
    /// The "why" is the point. A build report that says <c>nexui.binding.command: included</c>
    /// is not actionable; one that says <c>included because MainMenu/StartButton binds
    /// Game.Start</c> lets someone decide whether that screen should be doing that at all.
    /// Every entry therefore carries the node that caused it.
    /// </remarks>
    [Serializable]
    public sealed class NexFeatureManifest
    {
        [Serializable]
        public struct Requirement
        {
            public string FeatureId;

            /// <summary>Authoring node id that pulled the feature in.</summary>
            public string NodeId;

            /// <summary>Human-readable justification, written into the build report verbatim.</summary>
            public string Reason;
        }

        public List<Requirement> Requirements = new List<Requirement>();

        public void Require(string featureId, string nodeId, string reason)
        {
            if (string.IsNullOrEmpty(featureId)) return;

            // First cause wins: the report explains why a feature is in the build at all, and
            // the earliest node is the most useful example. Later nodes add nothing but noise.
            for (int i = 0; i < Requirements.Count; i++)
                if (Requirements[i].FeatureId == featureId) return;

            Requirements.Add(new Requirement
            {
                FeatureId = featureId,
                NodeId = nodeId ?? string.Empty,
                Reason = reason ?? string.Empty
            });
        }

        public bool Requires(string featureId)
        {
            for (int i = 0; i < Requirements.Count; i++)
                if (Requirements[i].FeatureId == featureId) return true;
            return false;
        }

        /// <summary>Feature ids in the order they were first required.</summary>
        public IEnumerable<string> FeatureIds
        {
            get
            {
                for (int i = 0; i < Requirements.Count; i++)
                    yield return Requirements[i].FeatureId;
            }
        }
    }
}
