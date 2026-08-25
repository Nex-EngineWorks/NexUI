using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Places children around a circle or arc - radial menus, ability wheels, orbiting markers.
    /// There is no uGUI equivalent at all.
    /// </summary>
    [AddComponentMenu("NexUI/Layout/NX Radial Layout")]
    public sealed class NXRadialLayoutGroup : LayoutGroup
    {
        [SerializeField] private float m_Radius = 120f;
        [SerializeField, Range(-360f, 360f)] private float m_StartAngle = 90f;
        [SerializeField, Range(-360f, 360f), Tooltip("Sweep covered by all children. 360 spreads them evenly around.")]
        private float m_SweepAngle = 360f;
        [SerializeField, Tooltip("Rotate each child so it faces outward from the centre.")]
        private bool m_RotateChildren;

        public override void CalculateLayoutInputVertical() { }
        public override void SetLayoutHorizontal() => Place();
        public override void SetLayoutVertical() => Place();

        private void Place()
        {
            var count = rectChildren.Count;
            if (count == 0) return;

            // A full sweep must not place the first and last child on top of each other, while a partial
            // arc should reach its end angle exactly.
            var full = Mathf.Abs(Mathf.Abs(m_SweepAngle) - 360f) < 0.01f;
            var divisor = full ? count : Mathf.Max(1, count - 1);

            for (var i = 0; i < count; i++)
            {
                var child = rectChildren[i];
                var angle = m_StartAngle + m_SweepAngle * (i / (float)divisor);
                var radians = angle * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * m_Radius;

                var width = LayoutUtility.GetPreferredWidth(child);
                var height = LayoutUtility.GetPreferredHeight(child);
                var centreX = rectTransform.rect.width * 0.5f;
                var centreY = rectTransform.rect.height * 0.5f;

                SetChildAlongAxis(child, 0, centreX + offset.x - width * 0.5f, width);
                SetChildAlongAxis(child, 1, centreY - offset.y - height * 0.5f, height);

                if (m_RotateChildren)
                    child.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
        }
    }
}
