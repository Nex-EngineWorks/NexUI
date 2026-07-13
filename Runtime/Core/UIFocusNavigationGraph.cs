using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Core
{
    public enum UIFocusDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>One element's explicit directional navigation links, authored in the Designer (<c>DesignerFocusMetadata</c>) and compiled here for runtime resolution.</summary>
    [Serializable]
    public struct UIFocusLink
    {
        public string elementId;
        public string upElementId;
        public string downElementId;
        public string leftElementId;
        public string rightElementId;
    }

    /// <summary>
    /// Resolves gamepad/keyboard directional navigation (brief §29) by following explicit links -
    /// no implicit geometry-based fallback here (that ambiguity - which axis convention a given
    /// backend uses for "up" - belongs in the Designer's own auto-generate step, which knows its
    /// coordinate space; see <c>FocusNavigationAutoLayout</c> in the Designer package). A screen
    /// with no authored links for the current element simply doesn't move focus in that direction,
    /// which is the correct, safe default rather than guessing.
    /// </summary>
    public sealed class UIFocusNavigationGraph
    {
        private readonly Dictionary<string, UIFocusLink> _links = new Dictionary<string, UIFocusLink>();
        private string _defaultElementId;

        public string DefaultElementId => _defaultElementId;

        public void SetLinks(IEnumerable<UIFocusLink> links, string defaultElementId = null)
        {
            _links.Clear();
            _defaultElementId = defaultElementId;
            if (links == null) return;
            foreach (var link in links)
                if (!string.IsNullOrEmpty(link.elementId))
                    _links[link.elementId] = link;
        }

        public void Clear()
        {
            _links.Clear();
            _defaultElementId = null;
        }

        public string Resolve(string currentElementId, UIFocusDirection direction)
        {
            if (string.IsNullOrEmpty(currentElementId) || !_links.TryGetValue(currentElementId, out var link))
                return null;

            switch (direction)
            {
                case UIFocusDirection.Up: return NullIfEmpty(link.upElementId);
                case UIFocusDirection.Down: return NullIfEmpty(link.downElementId);
                case UIFocusDirection.Left: return NullIfEmpty(link.leftElementId);
                case UIFocusDirection.Right: return NullIfEmpty(link.rightElementId);
                default: return null;
            }
        }

        /// <summary>True if every reachable-by-navigation element can be reached from <see cref="DefaultElementId"/> by following links (brief's "Unreachable Element Detection"); elements with no links at all are reported separately by the caller, not counted as unreachable here.</summary>
        public IReadOnlyCollection<string> FindUnreachableFrom(string startElementId)
        {
            var reachable = new HashSet<string>();
            if (!string.IsNullOrEmpty(startElementId) && _links.ContainsKey(startElementId))
            {
                var stack = new Stack<string>();
                stack.Push(startElementId);
                reachable.Add(startElementId);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (!_links.TryGetValue(current, out var link)) continue;
                    TryVisit(link.upElementId, reachable, stack);
                    TryVisit(link.downElementId, reachable, stack);
                    TryVisit(link.leftElementId, reachable, stack);
                    TryVisit(link.rightElementId, reachable, stack);
                }
            }

            var unreachable = new List<string>();
            foreach (var id in _links.Keys)
                if (!reachable.Contains(id))
                    unreachable.Add(id);
            return unreachable;
        }

        private static void TryVisit(string id, HashSet<string> reachable, Stack<string> stack)
        {
            if (string.IsNullOrEmpty(id) || !reachable.Add(id)) return;
            stack.Push(id);
        }

        private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
