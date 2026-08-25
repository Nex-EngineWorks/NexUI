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
    /// Placeholder shown while real content loads - the grey blocks that keep a list from jumping
    /// around once data arrives.
    /// </summary>
    /// <remarks>
    /// Toggling <see cref="Active"/> swaps the placeholder for the real content rather than only
    /// hiding itself, because the two always move together and leaving that to each caller is how
    /// screens end up showing both at once for a frame.
    /// </remarks>
    [AddComponentMenu("NexUI/Feedback/NX Skeleton")]
    public sealed class NXSkeleton : UIBehaviour, INXSkeleton
    {
        [SerializeField, Tooltip("Shown while loading.")] private GameObject m_Placeholder;
        [SerializeField, Tooltip("Shown once loading finished.")] private GameObject m_Content;
        [SerializeField] private bool m_Active = true;
        [SerializeField, Tooltip("Shimmer sweeps per second. 0 disables the shimmer entirely.")]
        private float m_ShimmerSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float m_ShimmerDepth = 0.35f;
        [SerializeField] private Graphic m_ShimmerTarget;

        private float _phase;
        private float _baseAlpha = 1f;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public bool Active
        {
            get => m_Active;
            set
            {
                if (m_Active == value) return;
                m_Active = value;
                Apply();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (m_ShimmerTarget != null) _baseAlpha = m_ShimmerTarget.color.a;
            Apply();
        }

        private void Apply()
        {
            if (m_Placeholder != null) m_Placeholder.SetActive(m_Active);
            if (m_Content != null) m_Content.SetActive(!m_Active);
        }

        private void Update()
        {
            if (!m_Active || m_ShimmerTarget == null || m_ShimmerSpeed <= 0f) return;

            _phase = Mathf.Repeat(_phase + m_ShimmerSpeed * UnityTime.unscaledDeltaTime, 1f);
            var wave = (Mathf.Sin(_phase * Mathf.PI * 2f) + 1f) * 0.5f;
            var color = m_ShimmerTarget.color;
            color.a = _baseAlpha * Mathf.Lerp(1f - m_ShimmerDepth, 1f, wave);
            m_ShimmerTarget.color = color;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_ShimmerTarget == null) return;
            var color = m_ShimmerTarget.color;
            color.a = _baseAlpha;
            m_ShimmerTarget.color = color;
        }
    }
}
