using System;
using System.Threading;
using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using UnityEngine;

namespace emiteat.NexUI.Motion
{
    /// <summary>
    /// Connects one compiled node's motion variants to backend-neutral pointer and focus events.
    /// </summary>
    public sealed class CompiledMotionBinding : IDisposable
    {
        private readonly IUIElementHandle _target;
        private readonly UIMotionPreset _preset;
        private readonly IUIMotionPlayer _player;
        private readonly MotionCompilerCache _cache;
        private readonly string _initial;
        private readonly string _animate;
        private readonly string _exit;
        private readonly string _hover;
        private readonly string _pressed;
        private readonly string _focus;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private IUIPointerCapability _pointer;
        private IUIFocusCapability _focusCapability;
        private int _playVersion;
        private bool _attached;
        private bool _disposed;

        public CompiledMotionBinding(
            IUIElementHandle target,
            UIMotionPreset preset,
            IUIMotionPlayer player,
            string initialVariant,
            string animateVariant,
            string exitVariant,
            string hoverVariant,
            string pressedVariant,
            string focusVariant,
            MotionCompilerCache cache = null)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _preset = preset ?? throw new ArgumentNullException(nameof(preset));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _cache = cache ?? new MotionCompilerCache();
            _initial = initialVariant;
            _animate = animateVariant;
            _exit = exitVariant;
            _hover = hoverVariant;
            _pressed = pressedVariant;
            _focus = focusVariant;
        }

        public CompiledMotionBinding Attach()
        {
            if (_attached || _disposed) return this;
            _attached = true;

            _pointer = _target.As<IUIPointerCapability>();
            if (_pointer != null)
            {
                _pointer.PointerEntered += OnPointerEntered;
                _pointer.PointerExited += OnPointerExited;
                _pointer.PointerDown += OnPointerDown;
                _pointer.PointerUp += OnPointerUp;
            }

            _focusCapability = _target.As<IUIFocusCapability>();
            if (_focusCapability != null)
            {
                _focusCapability.Focused += OnFocused;
                _focusCapability.Blurred += OnBlurred;
            }

            return this;
        }

        /// <summary>Plays initial, then animate, unless a newer state event takes ownership.</summary>
        public Task PlayEntryAsync()
            => PlaySequenceAsync(_initial, _animate);

        /// <summary>
        /// Plays the authored exit variant. Screen owners should await this before disposing the
        /// runtime; immediate disposal intentionally cancels active motion.
        /// </summary>
        public Task PlayExitAsync()
            => PlaySequenceAsync(_exit);

        private void OnPointerEntered() => Observe(PlaySequenceAsync(_hover));
        private void OnPointerExited() => Observe(PlaySequenceAsync(_animate));
        private void OnPointerDown() => Observe(PlaySequenceAsync(_pressed));
        private void OnPointerUp() => Observe(PlaySequenceAsync(
            !string.IsNullOrEmpty(_hover) ? _hover : _animate));
        private void OnFocused() => Observe(PlaySequenceAsync(_focus));
        private void OnBlurred() => Observe(PlaySequenceAsync(_animate));

        private async Task PlaySequenceAsync(params string[] variants)
        {
            if (_disposed || variants == null) return;
            var version = ++_playVersion;

            for (var i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (string.IsNullOrEmpty(variant)) continue;
                if (_disposed || version != _playVersion) return;

                var timeline = _cache.GetOrCompile(_preset, variant);
                if (timeline == null || timeline.Tracks == null || timeline.Tracks.Length == 0)
                    continue;

                await _player.PlayAsync(_target, timeline, _lifetime.Token);
            }
        }

        private static async void Observe(Task task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _playVersion++;

            if (_pointer != null)
            {
                _pointer.PointerEntered -= OnPointerEntered;
                _pointer.PointerExited -= OnPointerExited;
                _pointer.PointerDown -= OnPointerDown;
                _pointer.PointerUp -= OnPointerUp;
            }

            if (_focusCapability != null)
            {
                _focusCapability.Focused -= OnFocused;
                _focusCapability.Blurred -= OnBlurred;
            }

            _lifetime.Cancel();
            _player.Stop(_target);
            _lifetime.Dispose();
        }
    }
}
