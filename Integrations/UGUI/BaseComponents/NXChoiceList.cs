using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// A list of choices with single or multiple selection - difficulty pickers, graphics presets,
    /// quest filters. uGUI has ToggleGroup, which enforces exactly one and reports nothing useful.
    /// </summary>
    /// <remarks>
    /// Option views are pooled from a template rather than rebuilt, so changing the options of a
    /// settings row does not allocate a new hierarchy every time. Selection is stored as indices
    /// because that is what a binding writes back - option text is display, not identity.
    /// </remarks>
    [AddComponentMenu("NexUI/Data/NX Choice List")]
    public sealed class NXChoiceList : UIBehaviour, INXChoiceList
    {
        [SerializeField] private bool m_AllowMultiple;
        [SerializeField, Tooltip("Toggle used as the template for each option. Kept inactive.")]
        private Toggle m_OptionTemplate;
        [SerializeField, Tooltip("Parent the option views are created under. Defaults to this element.")]
        private RectTransform m_Container;
        [SerializeField] private List<string> m_Options = new List<string>();

        [SerializeField] private UnityEvent<int> m_OnSelectionChanged = new UnityEvent<int>();

        private readonly List<Toggle> _views = new List<Toggle>();
        private readonly List<int> _selected = new List<int>();
        private bool _suppress;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public event System.Action<IReadOnlyList<int>> SelectionChanged;

        /// <summary>Inspector-friendly mirror of <see cref="SelectionChanged"/>, carrying the first index.</summary>
        public UnityEvent<int> OnSelectionChanged => m_OnSelectionChanged;

        /// <inheritdoc/>
        public bool AllowMultiple
        {
            get => m_AllowMultiple;
            set
            {
                m_AllowMultiple = value;
                if (value || _selected.Count <= 1) return;

                // Collapsing to single selection keeps the first choice: it is the one the user
                // made earliest, and silently keeping the last would undo a deliberate pick.
                var keep = _selected[0];
                _selected.Clear();
                _selected.Add(keep);
                SyncViews();
                Raise();
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> Options => m_Options;

        /// <inheritdoc/>
        public IReadOnlyList<int> SelectedIndices => _selected;

        protected override void Awake()
        {
            base.Awake();
            if (m_Container == null) m_Container = transform as RectTransform;
            if (m_OptionTemplate != null) m_OptionTemplate.gameObject.SetActive(false);
            Rebuild();
        }

        /// <inheritdoc/>
        public void SetOptions(IReadOnlyList<string> options)
        {
            m_Options.Clear();
            if (options != null)
                for (var i = 0; i < options.Count; i++) m_Options.Add(options[i]);

            _selected.Clear();
            Rebuild();
            Raise();
        }

        /// <inheritdoc/>
        public void Select(int index, bool selected)
        {
            if (index < 0 || index >= m_Options.Count) return;

            var changed = false;
            if (selected)
            {
                if (!m_AllowMultiple && (_selected.Count != 1 || _selected[0] != index))
                {
                    _selected.Clear();
                    _selected.Add(index);
                    changed = true;
                }
                else if (m_AllowMultiple && !_selected.Contains(index))
                {
                    _selected.Add(index);
                    changed = true;
                }
            }
            else
            {
                changed = _selected.Remove(index);
            }

            if (!changed) return;
            SyncViews();
            Raise();
        }

        private void Rebuild()
        {
            for (var i = 0; i < _views.Count; i++)
                if (_views[i] != null) Destroy(_views[i].gameObject);
            _views.Clear();

            if (m_OptionTemplate == null || m_Container == null) return;

            for (var i = 0; i < m_Options.Count; i++)
            {
                var view = Instantiate(m_OptionTemplate, m_Container);
                view.gameObject.SetActive(true);
                view.gameObject.name = "Option " + i;

                var label = view.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null) label.text = m_Options[i];

                var index = i;
                view.onValueChanged.AddListener(on =>
                {
                    if (_suppress) return;
                    Select(index, on);
                });
                _views.Add(view);
            }

            SyncViews();
        }

        private void SyncViews()
        {
            // Writing isOn fires onValueChanged, which would call back into Select and fight the
            // change already in progress. The flag is what keeps one user click to one event.
            _suppress = true;
            for (var i = 0; i < _views.Count; i++)
                if (_views[i] != null) _views[i].isOn = _selected.Contains(i);
            _suppress = false;
        }

        private void Raise()
        {
            SelectionChanged?.Invoke(_selected);
            m_OnSelectionChanged.Invoke(_selected.Count > 0 ? _selected[0] : -1);
        }
    }
}
