using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Animates a number toward its target instead of snapping - score, currency and XP readouts.
    /// Formatting is part of the component so every call site does not re-implement grouping.
    /// </summary>
    [AddComponentMenu("NexUI/Text/NX Number Ticker")]
    public sealed class NXNumberTicker : NXTextBehaviour
    {
        [SerializeField] private double m_Value;
        [SerializeField, Tooltip("Seconds to travel the whole remaining distance.")]
        private float m_Duration = 0.4f;
        [SerializeField] private string m_Format = "N0";
        [SerializeField] private string m_Prefix = "";
        [SerializeField] private string m_Suffix = "";
        [SerializeField, Tooltip("Ignore Time.timeScale so the count still runs while paused.")]
        private bool m_Unscaled = true;

        private double _displayed;
        private double _target;
        private double _velocity;

        public double Value
        {
            get => _target;
            set => SetValue(value, animate: true);
        }

        private void OnEnable()
        {
            Resolve();
            _target = m_Value;
            _displayed = m_Value;
            Render();
        }

        public void SetValue(double value, bool animate)
        {
            _target = value;
            m_Value = value;
            if (animate && m_Duration > 0f) return;
            _displayed = value;
            Render();
        }

        private void Update()
        {
            if (Math.Abs(_target - _displayed) < 0.001d) return;

            var delta = m_Unscaled ? UnityTime.unscaledDeltaTime : UnityTime.deltaTime;
            var step = (_target - _displayed) * Mathf.Clamp01(delta / Mathf.Max(0.0001f, m_Duration));
            _displayed += step;
            if (Math.Abs(_target - _displayed) < 0.5d) _displayed = _target;
            Render();
        }

        private void Render()
        {
            if (!HasText) return;
            TextValue = m_Prefix + _displayed.ToString(m_Format, CultureInfo.CurrentCulture) + m_Suffix;
        }
    }
}
