using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Insets a RectTransform by the device safe area (notch, punch-hole, home indicator, rounded
    /// corners). Unity exposes <see cref="Screen.safeArea"/> but ships no component that applies it,
    /// so every mobile project rewrites this.
    /// </summary>
    [AddComponentMenu("NexUI/Layout/NX Safe Area")]
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class NXSafeArea : UIBehaviour
    {
        [SerializeField] private bool m_Left = true;
        [SerializeField] private bool m_Right = true;
        [SerializeField] private bool m_Top = true;
        [SerializeField] private bool m_Bottom = true;
        [SerializeField, Tooltip("Extra inset applied on top of the device safe area.")]
        private RectOffset m_AdditionalPadding;

        private Rect _appliedSafeArea;
        private Vector2Int _appliedScreen;

        protected override void OnEnable()
        {
            base.OnEnable();
            Apply(force: true);
        }

        private void Update()
        {
            // Safe area changes on rotation and on foldable posture changes, neither of which raises an
            // event, so this polls - cheaply, by comparing the values first.
            Apply(force: false);
        }

        public void Apply(bool force)
        {
            var safeArea = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == _appliedSafeArea && screen == _appliedScreen) return;
            if (screen.x <= 0 || screen.y <= 0) return;

            _appliedSafeArea = safeArea;
            _appliedScreen = screen;

            var min = safeArea.position;
            var max = safeArea.position + safeArea.size;
            min.x /= screen.x; min.y /= screen.y;
            max.x /= screen.x; max.y /= screen.y;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(m_Left ? min.x : 0f, m_Bottom ? min.y : 0f);
            rect.anchorMax = new Vector2(m_Right ? max.x : 1f, m_Top ? max.y : 1f);

            var padding = m_AdditionalPadding;
            rect.offsetMin = padding == null ? Vector2.zero : new Vector2(padding.left, padding.bottom);
            rect.offsetMax = padding == null ? Vector2.zero : new Vector2(-padding.right, -padding.top);
        }
    }
}
