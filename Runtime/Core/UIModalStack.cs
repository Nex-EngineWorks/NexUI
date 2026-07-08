using System.Collections.Generic;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Tracks the currently open modal screens (top-most is the active modal).
    /// Used to decide input blocking and focus trapping for stacked modals.
    /// </summary>
    public sealed class UIModalStack
    {
        private readonly List<string> _modals = new List<string>();

        public int Count => _modals.Count;
        public bool HasModal => _modals.Count > 0;

        public bool TryGetTop(out string screenId)
        {
            if (_modals.Count == 0) { screenId = null; return false; }
            screenId = _modals[_modals.Count - 1];
            return true;
        }

        public void Push(string screenId)
        {
            if (!_modals.Contains(screenId))
                _modals.Add(screenId);
        }

        public void Remove(string screenId) => _modals.Remove(screenId);

        /// <summary>Bottom-to-top snapshot for debug/inspection.</summary>
        public IReadOnlyList<string> Snapshot() => _modals.ToArray();

        public void Clear() => _modals.Clear();
    }
}
