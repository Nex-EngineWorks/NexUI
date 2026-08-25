using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// A ring that fills by value - cast bars, ability charge, segmented-free progress. uGUI can do
    /// this with a filled Image, but only with a sprite that already looks like a ring.
    /// </summary>
    /// <remarks>
    /// Drawn as an annulus so the thickness is a property rather than a property of the artwork.
    /// That is the whole reason this exists: a filled Image needs a new sprite for every combination
    /// of radius and thickness, and a HUD ends up with a folder of near-identical rings.
    /// </remarks>
    [AddComponentMenu("NexUI/Feedback/NX Radial Fill")]
    public sealed class NXRadialFill : MaskableGraphic, INXRadialFill
    {
        [SerializeField, Range(0f, 1f)] private float m_Fill = 0.75f;
        [SerializeField, Tooltip("Ring thickness in pixels. 0 fills to the centre like a pie.")]
        private float m_Thickness = 12f;
        [SerializeField, Tooltip("Where the fill starts, in degrees. 90 is the top.")]
        private float m_StartAngle = 90f;
        [SerializeField] private bool m_Clockwise = true;
        [SerializeField, Range(8, 180), Tooltip("Segments over a full turn. Higher is smoother.")]
        private int m_Segments = 72;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public float Fill
        {
            get => m_Fill;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, m_Fill)) return;
                m_Fill = clamped;
                SetVerticesDirty();
            }
        }

        /// <inheritdoc/>
        public bool Clockwise
        {
            get => m_Clockwise;
            set { m_Clockwise = value; SetVerticesDirty(); }
        }

        public float Thickness
        {
            get => m_Thickness;
            set { m_Thickness = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (m_Fill <= 0f) return;

            var rect = GetPixelAdjustedRect();
            var centre = rect.center;
            var outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (outer <= 0f) return;

            // 0 thickness means "fill to the centre", which is a pie rather than a ring. Clamping
            // instead of special-casing keeps one code path for both.
            var inner = m_Thickness <= 0f ? 0f : Mathf.Clamp(outer - m_Thickness, 0f, outer);

            var steps = Mathf.Max(1, Mathf.CeilToInt(m_Segments * m_Fill));
            var sweep = 360f * m_Fill * (m_Clockwise ? -1f : 1f);

            for (var i = 0; i <= steps; i++)
            {
                var radians = (m_StartAngle + sweep * (i / (float)steps)) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                vertexHelper.AddVert(centre + direction * inner, color, Vector2.zero);
                vertexHelper.AddVert(centre + direction * outer, color, Vector2.one);

                if (i == 0) continue;
                var v = i * 2;
                vertexHelper.AddTriangle(v - 2, v - 1, v + 1);
                vertexHelper.AddTriangle(v - 2, v + 1, v);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_Fill = Mathf.Clamp01(m_Fill);
            SetVerticesDirty();
        }
#endif
    }
}
