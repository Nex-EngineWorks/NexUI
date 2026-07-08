using System.Collections.Generic;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Navigation back stack of screen ids. Screens opened with
    /// <see cref="UIOpenPolicy.StackPush"/> are pushed here so <c>BackAsync</c> can pop them.
    /// </summary>
    public sealed class UIBackStack
    {
        private readonly List<string> _stack = new List<string>();

        public int Count => _stack.Count;
        public bool IsEmpty => _stack.Count == 0;

        public void Push(string screenId)
        {
            // Avoid duplicate consecutive entries.
            if (_stack.Count > 0 && _stack[_stack.Count - 1] == screenId) return;
            _stack.Add(screenId);
        }

        public bool TryPeek(out string screenId)
        {
            if (_stack.Count == 0) { screenId = null; return false; }
            screenId = _stack[_stack.Count - 1];
            return true;
        }

        public bool TryPop(out string screenId)
        {
            if (_stack.Count == 0) { screenId = null; return false; }
            int last = _stack.Count - 1;
            screenId = _stack[last];
            _stack.RemoveAt(last);
            return true;
        }

        public void Remove(string screenId) => _stack.RemoveAll(s => s == screenId);

        /// <summary>Bottom-to-top snapshot for debug/inspection.</summary>
        public IReadOnlyList<string> Snapshot() => _stack.ToArray();

        public void Clear() => _stack.Clear();
    }
}
