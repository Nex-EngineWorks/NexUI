using System;

namespace emiteat.NexUI.Diagnostics
{
    /// <summary>
    /// Points a diagnostic back at the authoring record that caused it.
    /// </summary>
    /// <remarks>
    /// Deliberately identifier-only: it holds no asset path, no <c>UnityEngine.Object</c> and no
    /// index into a live document, so it stays valid across a domain reload and can be written to
    /// a build report or a JSON bundle. Turning it into something clickable is the editor's job -
    /// see <c>NexDiagnosticNavigator</c>.
    ///
    /// <see cref="NodeId"/> is the element's rename-proof <c>stableId</c>, never the user-facing
    /// <c>elementId</c>, so a diagnostic captured before a rename still resolves afterwards.
    /// </remarks>
    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    public struct NexSourceLocation : IEquatable<NexSourceLocation>
    {
        public static readonly NexSourceLocation None = default;

        /// <summary>Screen this location belongs to. Empty for project-wide diagnostics.</summary>
        public string ScreenId;

        /// <summary>Rename-proof element identity, or empty when the whole screen is the subject.</summary>
        public string NodeId;

        /// <summary>Human-readable hierarchy path, kept for reports where ids mean nothing.</summary>
        public string NodePath;

        /// <summary>Field or property on the node, when the diagnostic is that specific.</summary>
        public string Member;

        public NexSourceLocation(string screenId, string nodeId = null, string nodePath = null, string member = null)
        {
            ScreenId = screenId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            NodePath = nodePath ?? string.Empty;
            Member = member ?? string.Empty;
        }

        public bool IsNone =>
            string.IsNullOrEmpty(ScreenId) &&
            string.IsNullOrEmpty(NodeId) &&
            string.IsNullOrEmpty(NodePath) &&
            string.IsNullOrEmpty(Member);

        /// <summary>Returns a copy pointing at <paramref name="member"/> on the same node.</summary>
        public NexSourceLocation WithMember(string member)
            => new NexSourceLocation(ScreenId, NodeId, NodePath, member);

        /// <summary>
        /// Every field, for use as an identity key.
        /// </summary>
        /// <remarks>
        /// <see cref="ToString"/> is for display: it picks the most specific subject and drops the
        /// rest, so two different elements render identically whenever they share a node path.
        /// Using that string to decide whether two diagnostics are the same problem collapsed them
        /// into one - which is exactly what deduplication is supposed to avoid.
        /// </remarks>
        public string ToIdentity()
            => ScreenId + Separator + NodeId + Separator + NodePath + Separator + Member;

        /// <summary>
        /// Field separator for <see cref="ToIdentity"/>: the ASCII unit separator.
        /// </summary>
        /// <remarks>
        /// Written as an escape rather than as a literal control character, which is
        /// invisible in an editor and reads as a missing separator. It has to be something
        /// no screen id, node id or path can contain, or two different locations could
        /// produce the same key - the bug ToIdentity exists to fix.
        /// </remarks>
        private const char Separator = (char)0x1f;

        public override string ToString()
        {
            if (IsNone) return "<unknown>";

            var subject = !string.IsNullOrEmpty(NodePath) ? NodePath
                : !string.IsNullOrEmpty(NodeId) ? NodeId
                : ScreenId;

            if (!string.IsNullOrEmpty(Member))
                return subject + "." + Member;

            return !string.IsNullOrEmpty(ScreenId) && subject != ScreenId
                ? ScreenId + "/" + subject
                : subject;
        }

        public bool Equals(NexSourceLocation other)
            => ScreenId == other.ScreenId && NodeId == other.NodeId
               && NodePath == other.NodePath && Member == other.Member;

        public override bool Equals(object obj) => obj is NexSourceLocation other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ScreenId != null ? ScreenId.GetHashCode() : 0;
                hash = (hash * 397) ^ (NodeId != null ? NodeId.GetHashCode() : 0);
                hash = (hash * 397) ^ (NodePath != null ? NodePath.GetHashCode() : 0);
                hash = (hash * 397) ^ (Member != null ? Member.GetHashCode() : 0);
                return hash;
            }
        }
    }
}
