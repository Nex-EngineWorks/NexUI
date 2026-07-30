using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Shared plumbing for the text effects: they drive whatever text component is on the object,
    /// TextMeshPro or legacy Text, so a project does not have to pick one to use them.
    /// </summary>
    public abstract class NXTextBehaviour : MonoBehaviour
    {
        private TMP_Text _tmp;
        private Text _legacy;

        protected string TextValue
        {
            get => _tmp != null ? _tmp.text : _legacy != null ? _legacy.text : string.Empty;
            set
            {
                if (_tmp != null) _tmp.text = value;
                else if (_legacy != null) _legacy.text = value;
            }
        }

        protected virtual void Awake() => Resolve();

        protected void Resolve()
        {
            _tmp = GetComponent<TMP_Text>();
            if (_tmp == null) _legacy = GetComponent<Text>();
        }

        protected bool HasText => _tmp != null || _legacy != null;
    }

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

            if (_pauseTimer > 0f) { _pauseTimer -= Time.unscaledDeltaTime; return; }

            var position = _content.anchoredPosition;
            position.x += _direction * m_PixelsPerSecond * Time.unscaledDeltaTime;

            if (m_Loop)
            {
                if (position.x < -overflow) position.x = 0f;
            }
            else if (position.x < -overflow) { position.x = -overflow; _direction = 1f; _pauseTimer = m_PauseSeconds; }
            else if (position.x > 0f) { position.x = 0f; _direction = -1f; _pauseTimer = m_PauseSeconds; }

            _content.anchoredPosition = position;
        }
    }

    /// <summary>
    /// Reveals text character by character, with the pacing rules dialogue systems actually need:
    /// per-character delay, extra pause on punctuation, and a skip that completes the line.
    /// </summary>
    [AddComponentMenu("NexUI/Text/NX Typewriter Text")]
    public sealed class NXTypewriterText : NXTextBehaviour
    {
        [SerializeField] private float m_CharactersPerSecond = 30f;
        [SerializeField, Tooltip("Extra seconds held on . ! ? , ; :")]
        private float m_PunctuationPause = 0.12f;
        [SerializeField] private bool m_PlayOnEnable = true;
        [SerializeField, Tooltip("Ignore Time.timeScale so text still types while the game is paused.")]
        private bool m_Unscaled = true;

        private string _full = string.Empty;
        private float _revealed;
        private bool _playing;

        public event Action Completed;
        public bool IsPlaying => _playing;

        private void OnEnable()
        {
            Resolve();
            if (m_PlayOnEnable) Play(TextValue);
        }

        public void Play(string text)
        {
            _full = text ?? string.Empty;
            _revealed = 0f;
            _playing = _full.Length > 0;
            TextValue = string.Empty;
        }

        /// <summary>Completes the line immediately - the "press again to skip" half of the interaction.</summary>
        public void Skip()
        {
            if (!_playing) return;
            _revealed = _full.Length;
            TextValue = _full;
            _playing = false;
            Completed?.Invoke();
        }

        private void Update()
        {
            if (!_playing) return;

            var delta = m_Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            var index = Mathf.Clamp(Mathf.FloorToInt(_revealed), 0, Mathf.Max(0, _full.Length - 1));
            var pause = _full.Length > 0 && IsPunctuation(_full[index]) ? m_PunctuationPause : 0f;

            _revealed += delta * Mathf.Max(1f, m_CharactersPerSecond) / (1f + pause * m_CharactersPerSecond);

            var count = Mathf.Clamp(Mathf.FloorToInt(_revealed), 0, _full.Length);
            TextValue = _full.Substring(0, count);

            if (count < _full.Length) return;
            _playing = false;
            Completed?.Invoke();
        }

        private static bool IsPunctuation(char c) => c is '.' or '!' or '?' or ',' or ';' or ':';
    }

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

            var delta = m_Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
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
