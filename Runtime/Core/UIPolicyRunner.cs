using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Applies screen policies that affect global runtime state: cursor, time scale
    /// and input blocking behind a screen. Reference counts across open screens so
    /// closing one screen does not clobber a policy another still requires.
    /// </summary>
    public sealed class UIPolicyRunner
    {
        private readonly HashSet<string> _pausers = new HashSet<string>();
        private readonly HashSet<string> _cursorShowers = new HashSet<string>();
        private float _cachedTimeScale = 1f;

        public void Apply(UIScreenInstance instance)
        {
            var p = instance.Definition.policy;

            if (p.pauseGameBehind || p.timePolicy == UITimePolicy.PauseWhileOpen)
            {
                if (_pausers.Count == 0)
                    _cachedTimeScale = Time.timeScale;
                _pausers.Add(instance.ScreenId);
                Time.timeScale = 0f;
            }

            ApplyCursor(instance.ScreenId, p.cursorPolicy);
        }

        public void Revert(UIScreenInstance instance)
        {
            var p = instance.Definition.policy;

            if (_pausers.Remove(instance.ScreenId) && _pausers.Count == 0)
                Time.timeScale = _cachedTimeScale;

            if (_cursorShowers.Remove(instance.ScreenId) && _cursorShowers.Count == 0)
            {
                // Nothing forcing the cursor visible anymore; leave as-is.
            }

            _ = p; // policy currently only affects pause + cursor at runtime.
        }

        private void ApplyCursor(string screenId, CursorPolicy policy)
        {
            switch (policy)
            {
                case CursorPolicy.ForceVisible:
                    _cursorShowers.Add(screenId);
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
            _pausers.Clear();
            _cursorShowers.Clear();
            Time.timeScale = _cachedTimeScale;
        }
    }
}
