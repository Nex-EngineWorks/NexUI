using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Label that scrolls its text when it overflows, instead of clipping or ellipsizing it.
    /// </summary>
    [UxmlElement]
    public partial class NXMarqueeLabel : VisualElement
    {
        private readonly Label _label = new Label();
        private float _offset;
        private float _direction = -1f;
        private float _pauseUntil;

        [UxmlAttribute] public string text
        {
            get => _label.text;
            set => _label.text = value;
        }
        [UxmlAttribute] public float pixelsPerSecond { get; set; } = 40f;
        [UxmlAttribute, Tooltip("Seconds held at each end before scrolling back.")]
        public float pauseSeconds { get; set; } = 1f;
        [UxmlAttribute] public bool loop { get; set; }

        public NXMarqueeLabel()
        {
            style.overflow = Overflow.Hidden;
            _label.style.position = Position.Absolute;
            _label.style.whiteSpace = WhiteSpace.NoWrap;
            Add(_label);
            schedule.Execute(Step).Every(16);
        }

        private void Step()
        {
            var overflow = _label.resolvedStyle.width - resolvedStyle.width;
            if (float.IsNaN(overflow) || overflow <= 0f)
            {
                _offset = 0f;
                _label.style.left = 0f;
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now < _pauseUntil) return;

            _offset += _direction * pixelsPerSecond * 0.016f;
            if (loop)
            {
                if (_offset < -overflow) _offset = 0f;
            }
            else if (_offset < -overflow) { _offset = -overflow; _direction = 1f; _pauseUntil = now + pauseSeconds; }
            else if (_offset > 0f) { _offset = 0f; _direction = -1f; _pauseUntil = now + pauseSeconds; }

            _label.style.left = _offset;
        }
    }

    /// <summary>Label that reveals its text character by character, with a skip.</summary>
    [UxmlElement]
    public partial class NXTypewriterLabel : VisualElement
    {
        private readonly Label _label = new Label();
        private string _full = string.Empty;
        private float _revealed;
        private bool _playing;

        [UxmlAttribute] public string text
        {
            get => _full;
            set => Play(value);
        }
        [UxmlAttribute] public float charactersPerSecond { get; set; } = 30f;
        [UxmlAttribute] public bool playOnAttach { get; set; } = true;

        public event Action Completed;
        public bool IsPlaying => _playing;

        public NXTypewriterLabel()
        {
            _label.style.whiteSpace = WhiteSpace.Normal;
            Add(_label);
            RegisterCallback<AttachToPanelEvent>(_ => { if (playOnAttach) Play(_full); });
            schedule.Execute(Step).Every(16);
        }

        public void Play(string value)
        {
            _full = value ?? string.Empty;
            _revealed = 0f;
            _playing = _full.Length > 0;
            _label.text = string.Empty;
        }

        /// <summary>Completes the line immediately - the "press again to skip" half of the interaction.</summary>
        public void Skip()
        {
            if (!_playing) return;
            _playing = false;
            _label.text = _full;
            Completed?.Invoke();
        }

        private void Step()
        {
            if (!_playing) return;
            _revealed += Mathf.Max(1f, charactersPerSecond) * 0.016f;
            var count = Mathf.Clamp(Mathf.FloorToInt(_revealed), 0, _full.Length);
            _label.text = _full.Substring(0, count);
            if (count < _full.Length) return;
            _playing = false;
            Completed?.Invoke();
        }
    }

    /// <summary>Label that counts toward its target value instead of snapping to it.</summary>
    [UxmlElement]
    public partial class NXNumberTickerLabel : VisualElement
    {
        private readonly Label _label = new Label();
        private double _displayed;
        private double _target;

        [UxmlAttribute] public double value
        {
            get => _target;
            set => SetValue(value, animate: true);
        }
        [UxmlAttribute, Tooltip("Seconds to travel the whole remaining distance.")]
        public float duration { get; set; } = 0.4f;
        [UxmlAttribute] public string format { get; set; } = "N0";
        [UxmlAttribute] public string prefix { get; set; } = "";
        [UxmlAttribute] public string suffix { get; set; } = "";

        public NXNumberTickerLabel()
        {
            Add(_label);
            schedule.Execute(Step).Every(16);
        }

        public void SetValue(double newValue, bool animate)
        {
            _target = newValue;
            if (!animate || duration <= 0f) _displayed = newValue;
            Render();
        }

        private void Step()
        {
            if (Math.Abs(_target - _displayed) < 0.001d) return;
            _displayed += (_target - _displayed) * Mathf.Clamp01(0.016f / Mathf.Max(0.0001f, duration));
            if (Math.Abs(_target - _displayed) < 0.5d) _displayed = _target;
            Render();
        }

        private void Render()
            => _label.text = prefix + _displayed.ToString(format, CultureInfo.CurrentCulture) + suffix;
    }

    /// <summary>
    /// Button that must be held before it fires, reporting progress meanwhile. UI Toolkit's Button
    /// only knows click, so hold-to-confirm interactions are hand-built every time.
    /// </summary>
    [UxmlElement]
    public partial class NXHoldButton : Button
    {
        [UxmlAttribute] public float holdSeconds { get; set; } = 1f;

        private float _startTime;
        private bool _holding;

        public event Action<float> Progress;
        public event Action HoldCompleted;

        public NXHoldButton()
        {
            RegisterCallback<PointerDownEvent>(_ => { _holding = true; _startTime = Time.realtimeSinceStartup; });
            RegisterCallback<PointerUpEvent>(_ => Cancel());
            RegisterCallback<PointerLeaveEvent>(_ => Cancel());
            schedule.Execute(Step).Every(16);
        }

        private void Cancel()
        {
            if (!_holding) return;
            _holding = false;
            Progress?.Invoke(0f);
        }

        private void Step()
        {
            if (!_holding) return;
            var elapsed = Time.realtimeSinceStartup - _startTime;
            var normalized = holdSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / holdSeconds);
            Progress?.Invoke(normalized);
            if (normalized < 1f) return;

            _holding = false;
            HoldCompleted?.Invoke();
        }
    }

    /// <summary>
    /// Recognizes swipes on any element. Attach with <c>element.AddManipulator(new NXSwipeManipulator())</c>
    /// or use the Designer's Swipe Area component.
    /// </summary>
    public sealed class NXSwipeManipulator : PointerManipulator
    {
        public enum SwipeDirection { None, Left, Right, Up, Down }

        private readonly float _threshold;
        private Vector2 _start;
        private bool _tracking;

        public event Action<SwipeDirection> Swiped;

        public NXSwipeManipulator(float threshold = 60f) => _threshold = threshold;

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerUpEvent>(OnUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
        }

        private void OnDown(PointerDownEvent evt)
        {
            _start = evt.position;
            _tracking = true;
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (!_tracking) return;
            _tracking = false;

            var delta = (Vector2)evt.position - _start;
            if (delta.magnitude < _threshold) return;

            var horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            Swiped?.Invoke(horizontal
                ? delta.x > 0f ? SwipeDirection.Right : SwipeDirection.Left
                : delta.y > 0f ? SwipeDirection.Down : SwipeDirection.Up);
        }
    }
}
