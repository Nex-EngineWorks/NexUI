using System.Collections.Generic;
using emiteat.NexUI.Components;
using emiteat.NexUI.Integrations.UGUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Samples.CollectionDemo
{
    /// <summary>
    /// Drives an <see cref="NXCollectionView"/> with real data: ten thousand rows, selection,
    /// live updates and the loading/empty/error states.
    /// </summary>
    /// <remarks>
    /// Attach next to a <c>ScrollRect</c> that has <see cref="NXCollectionView"/> on it, assign the
    /// item template in the component, and press Play. The row count is the point: the pool only
    /// ever holds what the viewport shows, so ten thousand rows cost the same as twenty.
    /// </remarks>
    [AddComponentMenu("NexUI/Samples/Collection Demo (uGUI)")]
    public sealed class CollectionDemoUGUI : MonoBehaviour
    {
        [SerializeField, Tooltip("The collection to drive. Found on this object when left empty.")]
        private NXCollectionView m_Collection;

        [SerializeField, Tooltip("Rows to generate. Try 10000 - virtualization is what makes it cheap.")]
        private int m_ItemCount = 10_000;

        [SerializeField, Tooltip("Label that reports the selection and the realized window.")]
        private TMP_Text m_StatusLabel;

        private readonly NXCollectionSource<CollectionDemoItem> _source = new NXCollectionSource<CollectionDemoItem>();
        private List<CollectionDemoItem> _items;

        private void Awake()
        {
            if (m_Collection == null) m_Collection = GetComponent<NXCollectionView>();
            if (m_Collection == null)
            {
                Debug.LogError("[NexUI Sample] CollectionDemoUGUI needs an NXCollectionView on this object.", this);
                enabled = false;
                return;
            }

            // Bind runs against a recycled view, so it must set every field it cares about - there is
            // no "fresh row" to rely on defaults from.
            m_Collection.BindItem = (index, item, view) =>
            {
                var row = item as CollectionDemoItem;
                var label = view.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = row == null ? string.Empty : $"{row.Name}   x{row.Amount}   {row.Rarity}";

                var background = view.GetComponent<Image>();
                if (background != null)
                    background.color = m_Collection.Controller.IsSelected(index)
                        ? new Color(0.25f, 0.42f, 0.68f, 1f)
                        : new Color(0.13f, 0.16f, 0.22f, 1f);
            };

            m_Collection.Controller.SelectionChanged += _ => Report();
            m_Collection.Controller.ItemActivated += OnActivated;
            m_Collection.Controller.VisibleRangeChanged += _ => Report();
            m_Collection.Source = _source;
        }

        private void Start() => Load();

        /// <summary>Fills the collection, going through Loading first the way a real fetch would.</summary>
        public void Load()
        {
            m_Collection.State = NXCollectionState.Loading;
            _items = CollectionDemoData.Build(m_ItemCount);
            _source.Set(_items);
            m_Collection.State = NXCollectionState.Content;
            Report();
        }

        /// <summary>Empties the collection. The state resolves to Empty on its own.</summary>
        public void Clear()
        {
            _items = CollectionDemoData.Build(0);
            _source.Set(_items);
            Report();
        }

        /// <summary>Shows the error state, for checking that the error slot is wired.</summary>
        public void Fail() => m_Collection.State = NXCollectionState.Error;

        /// <summary>Removes the selected row, to show the selection surviving a data change.</summary>
        public void RemoveSelected()
        {
            var index = m_Collection.Controller.SelectedIndex;
            if (_items == null || index < 0 || index >= _items.Count) return;
            _items.RemoveAt(index);
            _source.Notify();
            Report();
        }

        /// <summary>Jumps to the middle, to show ScrollTo on a virtualized list.</summary>
        public void ScrollToMiddle()
        {
            if (_items == null || _items.Count == 0) return;
            m_Collection.ScrollTo(_items.Count / 2, NXScrollAlignment.Center);
        }

        private void OnActivated(int index)
        {
            var item = _source.Get(index);
            if (item != null) Debug.Log($"[NexUI Sample] Activated {item}", this);
        }

        private void Report()
        {
            if (m_StatusLabel == null) return;
            var controller = m_Collection.Controller;
            m_StatusLabel.text =
                $"{controller.ItemCount} items | realized {controller.VisibleRange} | " +
                $"selected {controller.SelectedIndex} | {controller.State}";
        }
    }
}
