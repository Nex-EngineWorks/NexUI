using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Components
{
    /// <summary>
    /// Generic recycle pool for list/grid item views (B4: performance - avoids
    /// instantiate/destroy churn every time an <see cref="INXList"/>/<see cref="INXGrid"/>
    /// implementation's <c>SetItems</c>/<c>Refresh</c> runs). Backend-agnostic:
    /// <typeparamref name="TView"/> is whatever the backend's item view type is (a component for
    /// uGUI, a <c>VisualElement</c> for UI Toolkit) - this pool only tracks active/inactive
    /// counts and calls the supplied factory/activation delegates, so it has no Unity type
    /// dependency itself and works from either Integration assembly.
    /// </summary>
    public sealed class UIItemPool<TView> where TView : class
    {
        private readonly Func<TView> _create;
        private readonly Action<TView, bool> _setActive;
        private readonly List<TView> _pool = new List<TView>();
        private int _activeCount;

        public UIItemPool(Func<TView> create, Action<TView, bool> setActive)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _setActive = setActive ?? throw new ArgumentNullException(nameof(setActive));
        }

        public int ActiveCount => _activeCount;
        public int PooledCount => _pool.Count;

        /// <summary>
        /// Grows/shrinks the active window to exactly <paramref name="count"/> views, reusing
        /// pooled (previously deactivated) instances before creating new ones, and never
        /// destroying a view once created. <paramref name="onActive"/> is called once per active
        /// index/view pair so the caller can bind item data onto it.
        /// </summary>
        public void Resize(int count, Action<int, TView> onActive)
        {
            if (count < 0) count = 0;

            while (_pool.Count < count)
                _pool.Add(_create());

            for (int i = 0; i < count; i++)
            {
                var view = _pool[i];
                if (i >= _activeCount) _setActive(view, true);
                onActive?.Invoke(i, view);
            }
            for (int i = count; i < _activeCount; i++)
                _setActive(_pool[i], false);

            _activeCount = count;
        }

        /// <summary>Deactivates every currently active view without discarding the pool.</summary>
        public void Clear()
        {
            for (int i = 0; i < _activeCount; i++)
                _setActive(_pool[i], false);
            _activeCount = 0;
        }
    }
}
