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
