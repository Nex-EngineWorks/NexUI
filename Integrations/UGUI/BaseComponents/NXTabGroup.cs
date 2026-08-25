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
    /// Switches which page object is visible when its tab is selected. uGUI has ToggleGroup but no
    /// notion of tab content, so every project wires the show/hide by hand.
    /// </summary>
    [AddComponentMenu("NexUI/Data/NX Tab Group")]
    public sealed class NXTabGroup : UIBehaviour
    {
        [SerializeField, Tooltip("Tab buttons, in order. Each one selects the page at the same index.")]
        private List<Toggle> m_Tabs = new List<Toggle>();
        [SerializeField, Tooltip("Page roots, in the same order as the tabs.")]
        private List<GameObject> m_Pages = new List<GameObject>();
        [SerializeField] private int m_ActiveIndex;
        [SerializeField, Tooltip("Keep pages loaded and only toggle their visibility.")]
        private bool m_KeepPagesAlive = true;

        [SerializeField] private UnityEvent<int> m_OnTabChanged = new UnityEvent<int>();

        public UnityEvent<int> OnTabChanged => m_OnTabChanged;
        public int ActiveIndex => m_ActiveIndex;

        protected override void OnEnable()
        {
            base.OnEnable();
            for (var i = 0; i < m_Tabs.Count; i++)
            {
                var index = i;
                if (m_Tabs[i] == null) continue;
                m_Tabs[i].onValueChanged.AddListener(on => { if (on) Select(index); });
            }
            Select(m_ActiveIndex, notify: false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var tab in m_Tabs)
                if (tab != null) tab.onValueChanged.RemoveAllListeners();
        }

        public void Select(int index, bool notify = true)
        {
            if (m_Pages.Count == 0) return;
            m_ActiveIndex = Mathf.Clamp(index, 0, m_Pages.Count - 1);

            for (var i = 0; i < m_Pages.Count; i++)
            {
                if (m_Pages[i] == null) continue;
                var active = i == m_ActiveIndex;
                if (m_KeepPagesAlive) m_Pages[i].SetActive(active);
                else if (active) m_Pages[i].SetActive(true);
                else m_Pages[i].SetActive(false);
            }

            for (var i = 0; i < m_Tabs.Count; i++)
                if (m_Tabs[i] != null) m_Tabs[i].SetIsOnWithoutNotify(i == m_ActiveIndex);

            if (notify) m_OnTabChanged.Invoke(m_ActiveIndex);
        }
    }
}
