using System;
using System.Collections.Generic;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Paged carousel: snaps to whole pages, supports looping and auto-advance, and reports the page
    /// so indicators can follow. uGUI has ScrollRect but no paging at all.
    /// </summary>
    [AddComponentMenu("NexUI/Data/NX Carousel")]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class NXCarousel : UIBehaviour, IEndDragHandler
    {
        [SerializeField] private bool m_Horizontal = true;
        [SerializeField, Tooltip("Seconds between automatic page changes. 0 disables auto-advance.")]
        private float m_AutoAdvanceSeconds;
        [SerializeField] private bool m_Loop = true;
        [SerializeField, Tooltip("Seconds the snap animation takes.")]
        private float m_SnapDuration = 0.25f;

        [SerializeField] private UnityEvent<int> m_OnPageChanged = new UnityEvent<int>();

        private ScrollRect _scroll;
        private float _timer;
        private float _snapFrom;
        private float _snapTo;
        private float _snapElapsed = -1f;

        public UnityEvent<int> OnPageChanged => m_OnPageChanged;
        public int PageCount => _scroll != null && _scroll.content != null ? _scroll.content.childCount : 0;
        public int CurrentPage { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _scroll = GetComponent<ScrollRect>();
        }

        public void GoTo(int page, bool animate = true)
        {
            var count = PageCount;
            if (count <= 1) return;

            if (m_Loop) page = (page % count + count) % count;
            else page = Mathf.Clamp(page, 0, count - 1);

            CurrentPage = page;
            var target = count == 1 ? 0f : page / (float)(count - 1);

            if (!animate || m_SnapDuration <= 0f)
            {
                SetNormalized(target);
                m_OnPageChanged.Invoke(page);
                return;
            }

            _snapFrom = Normalized();
            _snapTo = target;
            _snapElapsed = 0f;
            m_OnPageChanged.Invoke(page);
        }

        public void Next() => GoTo(CurrentPage + 1);
        public void Previous() => GoTo(CurrentPage - 1);

        public void OnEndDrag(PointerEventData eventData)
        {
            var count = PageCount;
            if (count <= 1) return;
            // Snap to whichever page the drag ended nearest, which is what makes it feel paged rather
            // than free-scrolling.
            GoTo(Mathf.RoundToInt(Normalized() * (count - 1)));
        }

        private void Update()
        {
            if (_snapElapsed >= 0f)
            {
                _snapElapsed += UnityTime.unscaledDeltaTime;
                var t = Mathf.Clamp01(_snapElapsed / Mathf.Max(0.0001f, m_SnapDuration));
                SetNormalized(Mathf.Lerp(_snapFrom, _snapTo, Mathf.SmoothStep(0f, 1f, t)));
                if (t >= 1f) _snapElapsed = -1f;
                return;
            }

            if (m_AutoAdvanceSeconds <= 0f) return;
            _timer += UnityTime.unscaledDeltaTime;
            if (_timer < m_AutoAdvanceSeconds) return;
            _timer = 0f;
            Next();
        }

        private float Normalized()
            => m_Horizontal ? _scroll.horizontalNormalizedPosition : 1f - _scroll.verticalNormalizedPosition;

        private void SetNormalized(float value)
        {
            if (m_Horizontal) _scroll.horizontalNormalizedPosition = value;
            else _scroll.verticalNormalizedPosition = 1f - value;
        }
    }
}
