using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
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

            var delta = m_Unscaled ? UnityTime.unscaledDeltaTime : UnityTime.deltaTime;
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
}
