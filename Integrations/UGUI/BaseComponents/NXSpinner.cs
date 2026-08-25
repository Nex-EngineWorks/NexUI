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
    /// Indeterminate loading indicator: spins its own transform for as long as it is showing
    /// something the game cannot put a percentage on.
    /// </summary>
    /// <remarks>
    /// Unscaled time on purpose. A spinner that stops because the game paused - or because a loading
    /// screen set <c>timeScale</c> to zero - reads as a freeze, which is the exact impression the
    /// spinner exists to prevent.
    /// </remarks>
    [AddComponentMenu("NexUI/Feedback/NX Spinner")]
    public sealed class NXSpinner : UIBehaviour, INXSpinner
    {
        [SerializeField] private bool m_Spinning = true;
        [SerializeField, Tooltip("Degrees per second. Negative spins the other way.")]
        private float m_Speed = 240f;

        private RectTransform _rect;
        private float _angle;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public bool Spinning
        {
            get => m_Spinning;
            set => m_Spinning = value;
        }

        /// <inheritdoc/>
        public float Speed
        {
            get => m_Speed;
            set => m_Speed = value;
        }

        protected override void Awake()
        {
            base.Awake();
            _rect = transform as RectTransform;
        }

        private void Update()
        {
            if (!m_Spinning || _rect == null) return;
            _angle = Mathf.Repeat(_angle - m_Speed * UnityTime.unscaledDeltaTime, 360f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, _angle);
        }
    }
}
