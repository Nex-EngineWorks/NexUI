using System;
using UnityEngine;

namespace emiteat.NexUI.MotionGraph
{
    /// <summary>
    /// Typed data-flow + event graph asset (brief §11-27; Architecture-Audit.md Phase 5). Distinct
    /// from and coexisting with the older <c>emiteat.NexUI.Motion.UIMotionGraph</c> ordering-DAG -
    /// that type is unchanged and still works; this is the new engine, not a replacement in place.
    /// Pure data - execution lives in <see cref="UIGraphExecutor"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Motion Graph (v2)", fileName = "NewMotionGraph")]
    public sealed class UIMotionGraphAsset : ScriptableObject
    {
        public UIGraphNode[] nodes = Array.Empty<UIGraphNode>();

        /// <summary>Named entry points (e.g. "OnClick", "OnScreenOpen") mapped to the node id execution starts at.</summary>
        public UIGraphEntryPoint[] entryPoints = Array.Empty<UIGraphEntryPoint>();

        public UIGraphNode FindNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var node in nodes)
                if (node != null && node.id == id)
                    return node;
            return null;
        }

        public string FindEntryNodeId(string eventName)
        {
            foreach (var entry in entryPoints)
                if (entry.eventName == eventName)
                    return entry.nodeId;
            return null;
        }
    }

    [Serializable]
    public struct UIGraphEntryPoint
    {
        public string eventName;
        public string nodeId;
    }
}
