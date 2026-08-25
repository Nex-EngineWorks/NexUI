using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Scrolls text horizontally when it is longer than its box - the standard treatment for long
    /// item names and ticker lines, which uGUI has no answer for beyond truncating.
    /// </summary>
    [AddComponentMenu("NexUI/Text/NX Marquee Text")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class NXMarqueeText : NXTextBehaviour
    {
        [SerializeField] private float m_PixelsPerSecond = 40f;
        [SerializeField, Tooltip("Seconds to wait at each end before scrolling back.")]
        private float m_PauseSeconds = 1f;
        [SerializeField, Tooltip("Loop continuously instead of bouncing between the two ends.")]
        private bool m_Loop;
        [SerializeField, Tooltip("Only scroll while the pointer is over the element.")]
        private bool m_OnHoverOnly;

        private RectTransform _content;
        private RectTransform _viewport;
        private float _direction = -1f;
        private float _pauseTimer;
        private bool _hovered;

        protected override void Awake()
        {
            base.Awake();
            _viewport = (RectTransform)transform;
            _content = transform.childCount > 0 ? transform.GetChild(0) as RectTransform : _viewport;
        }

        public void SetHovered(bool hovered) => _hovered = hovered;

        private void Update()
        {
            if (_content == null || _content == _viewport) return;
            if (m_OnHoverOnly && !_hovered) return;

            var overflow = _content.rect.width - _viewport.rect.width;
            if (overflow <= 0f)
            {
                _content.anchoredPosition = new Vector2(0f, _content.anchoredPosition.y);
                return;
            }

            if (_pauseTimer > 0f) { _pauseTimer -= UnityTime.unscaledDeltaTime; return; }

            var position = _content.anchoredPosition;
            position.x += _direction * m_PixelsPerSecond * UnityTime.unscaledDeltaTime;

            if (m_Loop)
            {
                if (position.x < -overflow) position.x = 0f;
            }
            else if (position.x < -overflow) { position.x = -overflow; _direction = 1f; _pauseTimer = m_PauseSeconds; }
            else if (position.x > 0f) { position.x = 0f; _direction = -1f; _pauseTimer = m_PauseSeconds; }

            _content.anchoredPosition = position;
        }
    }
}
