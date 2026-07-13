using System;
using emiteat.NexUI.MotionClip;
using UnityEngine;

namespace emiteat.NexUI.MotionGraph
{
    /// <summary>
    /// Data-port types Motion Graph v2 nodes can pass between each other (brief §11). Trimmed to
    /// what Phase 5's node set (Sequence/Parallel/Delay/Branch/PlayClip) actually needs -
    /// ElementList/Screen/Command/Sprite/Audio join once the nodes that produce/consume them exist
    /// (Phase 6), rather than being declared now with no node ever setting them.
    /// </summary>
    public enum UIGraphValueType
    {
        Bool,
        Int,
        Float,
        String,
        Vector2,
        Vector3,
        Color,

        /// <summary>An element id (string) within the executing <see cref="Abstractions.IUISurface"/>.</summary>
        Element,
        MotionClip,

        /// <summary>A <see cref="UIMotionGraphAsset"/> reference, used by <c>Graph.RunSubgraph</c>.</summary>
        MotionGraph,
        Object
    }

    /// <summary>Tagged-union value flowing through a data port, mirroring <see cref="UIMotionClipValue"/>'s established pattern.</summary>
    [Serializable]
    public struct UIGraphValue
    {
        public UIGraphValueType type;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Color colorValue;
        public UIMotionClip motionClipValue;
        public UIMotionGraphAsset motionGraphValue;
        public UnityEngine.Object objectValue;

        public static UIGraphValue Bool(bool value) => new UIGraphValue { type = UIGraphValueType.Bool, boolValue = value };
        public static UIGraphValue Int(int value) => new UIGraphValue { type = UIGraphValueType.Int, intValue = value };
        public static UIGraphValue Float(float value) => new UIGraphValue { type = UIGraphValueType.Float, floatValue = value };
        public static UIGraphValue String(string value) => new UIGraphValue { type = UIGraphValueType.String, stringValue = value };
        public static UIGraphValue FromVector2(Vector2 value) => new UIGraphValue { type = UIGraphValueType.Vector2, vector2Value = value };
        public static UIGraphValue FromVector3(Vector3 value) => new UIGraphValue { type = UIGraphValueType.Vector3, vector3Value = value };
        public static UIGraphValue FromColor(Color value) => new UIGraphValue { type = UIGraphValueType.Color, colorValue = value };
        public static UIGraphValue Element(string elementId) => new UIGraphValue { type = UIGraphValueType.Element, stringValue = elementId };
        public static UIGraphValue Clip(UIMotionClip clip) => new UIGraphValue { type = UIGraphValueType.MotionClip, motionClipValue = clip };
        public static UIGraphValue Graph(UIMotionGraphAsset graph) => new UIGraphValue { type = UIGraphValueType.MotionGraph, motionGraphValue = graph };
        public static UIGraphValue FromObject(UnityEngine.Object value) => new UIGraphValue { type = UIGraphValueType.Object, objectValue = value };
    }
}
