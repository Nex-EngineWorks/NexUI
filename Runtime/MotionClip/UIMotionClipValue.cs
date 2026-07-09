using System;
using UnityEngine;

namespace emiteat.NexUI.MotionClip
{
    /// <summary>Discriminates which field of <see cref="UIMotionClipValue"/> is populated.</summary>
    public enum UIMotionClipValueType
    {
        Float = 0,
        Vector2 = 1,
        Vector3 = 2,
        Color = 3
    }

    /// <summary>
    /// Serializable tagged-union value used by <see cref="UIMotionClipKeyframe"/> so a single
    /// keyframe type can carry float, Vector2, Vector3, or Color payloads.
    /// </summary>
    [Serializable]
    public struct UIMotionClipValue
    {
        public UIMotionClipValueType valueType;
        public float floatValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Color colorValue;

        public static UIMotionClipValue Float(float value) => new UIMotionClipValue { valueType = UIMotionClipValueType.Float, floatValue = value };
        public static UIMotionClipValue FromVector2(Vector2 value) => new UIMotionClipValue { valueType = UIMotionClipValueType.Vector2, vector2Value = value };
        public static UIMotionClipValue FromVector3(Vector3 value) => new UIMotionClipValue { valueType = UIMotionClipValueType.Vector3, vector3Value = value };
        public static UIMotionClipValue FromColor(Color value) => new UIMotionClipValue { valueType = UIMotionClipValueType.Color, colorValue = value };

        public static UIMotionClipValue Lerp(UIMotionClipValue a, UIMotionClipValue b, float t)
        {
            switch (a.valueType)
            {
                case UIMotionClipValueType.Vector2:
                    return FromVector2(UnityEngine.Vector2.LerpUnclamped(a.vector2Value, b.vector2Value, t));
                case UIMotionClipValueType.Vector3:
                    return FromVector3(UnityEngine.Vector3.LerpUnclamped(a.vector3Value, b.vector3Value, t));
                case UIMotionClipValueType.Color:
                    return FromColor(UnityEngine.Color.LerpUnclamped(a.colorValue, b.colorValue, t));
                default:
                    return Float(Mathf.LerpUnclamped(a.floatValue, b.floatValue, t));
            }
        }
    }
}
