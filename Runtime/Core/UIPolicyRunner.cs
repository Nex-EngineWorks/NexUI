using System.Collections.Generic;
using UnityEngine;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Applies screen policies that affect global runtime state: cursor, time scale
    /// and input blocking behind a screen. Reference counts across open screens so
    /// closing one screen does not clobber a policy another still requires.
    /// </summary>
    public sealed class UIPolicyRunner
    {
        /// <summary>
        /// Time scale used while any <see cref="UITimePolicy.SlowWhileOpen"/> screen is open.
        /// Configurable because 0.25 is a taste, not physics - cinematic slow-mo often wants
        /// something else.
        /// </summary>
        public static float SlowWhileOpenScale = 0.25f;

        private readonly Dictionary<string, UITimePolicy> _timePolicies = new Dictionary<string, UITimePolicy>();
        private readonly Dictionary<string, CursorPolicy> _cursorPolicies = new Dictionary<string, CursorPolicy>();
        private readonly List<string> _cursorOrder = new List<string>();
        private float _cachedTimeScale = 1f;
        private bool _cachedCursorVisible;
        private CursorLockMode _cachedCursorLockState;

        public void Apply(UIScreenInstance instance)
        {
            var p = instance.Definition.policy;

            var timePolicy = p.pauseGameBehind ? UITimePolicy.PauseWhileOpen : p.timePolicy;
            if (timePolicy != UITimePolicy.Unchanged)
            {
                // Capture only on the 0→1 transition: the runner owns Time.timeScale while any
                // policy is active, so the value it restores is whatever the game had before the
                // FIRST policy engaged - not what some intermediate screen state wrote.
                if (_timePolicies.Count == 0)
                    _cachedTimeScale = UnityTime.timeScale;
                _timePolicies[instance.ScreenId] = timePolicy;
                ApplyTimePolicy();
            }

            ApplyCursor(instance.ScreenId, p.cursorPolicy);
        }

        public void Revert(UIScreenInstance instance)
        {
            var p = instance.Definition.policy;

            if (_timePolicies.Remove(instance.ScreenId))
            {
                if (_timePolicies.Count == 0) UnityTime.timeScale = _cachedTimeScale;
                else ApplyTimePolicy();
            }

            RevertCursor(instance.ScreenId);

        }

        private void ApplyTimePolicy()
        {
            foreach (var policy in _timePolicies.Values)
                if (policy == UITimePolicy.PauseWhileOpen)
                {
                    UnityTime.timeScale = 0f;
                    return;
                }
            UnityTime.timeScale = _cachedTimeScale * SlowWhileOpenScale;
        }

        private void ApplyCursor(string screenId, CursorPolicy policy)
        {
            if (policy == CursorPolicy.Unchanged) return;
            if (_cursorPolicies.Count == 0)
            {
                _cachedCursorVisible = Cursor.visible;
                _cachedCursorLockState = Cursor.lockState;
            }
            _cursorPolicies[screenId] = policy;
            _cursorOrder.Remove(screenId);
            _cursorOrder.Add(screenId);
            SetCursor(policy);
        }

        private void RevertCursor(string screenId)
        {
            if (!_cursorPolicies.Remove(screenId)) return;
            _cursorOrder.Remove(screenId);
            if (_cursorOrder.Count == 0)
            {
                Cursor.visible = _cachedCursorVisible;
                Cursor.lockState = _cachedCursorLockState;
                return;
            }
            SetCursor(_cursorPolicies[_cursorOrder[_cursorOrder.Count - 1]]);
        }

        private static void SetCursor(CursorPolicy policy)
        {
            switch (policy)
            {
                case CursorPolicy.ForceVisible:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    break;
                case CursorPolicy.ForceHidden:
                    Cursor.visible = false;
                    break;
                case CursorPolicy.LockCentered:
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;
                case CursorPolicy.Unchanged:
                default:
                    break;
            }
        }

        public void Reset()
        {
            if (_timePolicies.Count > 0) UnityTime.timeScale = _cachedTimeScale;
            _timePolicies.Clear();
            if (_cursorPolicies.Count > 0)
            {
                Cursor.visible = _cachedCursorVisible;
                Cursor.lockState = _cachedCursorLockState;
            }
            _cursorPolicies.Clear();
            _cursorOrder.Clear();
        }
    }
}
