using System.Collections.Generic;

namespace emiteat.NexUI.Core
{
    /// <summary>A queued toast request.</summary>
    public struct UIToastRequest
    {
        public string screenId;
        public UIOpenArgs args;
    }

    /// <summary>
    /// FIFO queue for toast-style screens (<see cref="UIOpenPolicy.Queue"/>), presented
    /// one at a time. The UIManager drains this after each toast finishes.
    /// </summary>
    public sealed class UIToastQueue
    {
        private readonly Queue<UIToastRequest> _queue = new Queue<UIToastRequest>();

        public int Count => _queue.Count;
        public bool IsEmpty => _queue.Count == 0;

        /// <summary>Screen id currently being presented, if any.</summary>
        public string ActiveScreenId { get; private set; }

        public void Enqueue(string screenId, UIOpenArgs args)
            => _queue.Enqueue(new UIToastRequest { screenId = screenId, args = args });

        public bool TryActivate(string screenId)
        {
            if (ActiveScreenId != null) return false;
            ActiveScreenId = screenId;
            return true;
        }

        public bool TryDequeue(out UIToastRequest request)
        {
            if (_queue.Count == 0) { request = default; return false; }
            request = _queue.Dequeue();
            ActiveScreenId = request.screenId;
            return true;
        }

        public void MarkFinished(string screenId)
        {
            if (ActiveScreenId == screenId)
                ActiveScreenId = null;
        }

        public void Clear()
        {
            _queue.Clear();
            ActiveScreenId = null;
        }
    }
}
