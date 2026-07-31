using System.Collections.Generic;
using emiteat.NexUI.Components;
using emiteat.NexUI.Integrations.UIToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Samples.CollectionDemo
{
    /// <summary>
    /// The same demo on UI Toolkit, driving an <see cref="NXCollectionViewElement"/>.
    /// </summary>
    /// <remarks>
    /// The data, the source and the event wiring are identical to the uGUI sample - only the item
    /// view differs, because that is the part a backend actually owns. Attach to a GameObject with a
    /// <c>UIDocument</c>; the collection is built in code so the sample needs no UXML asset.
    /// </remarks>
    [AddComponentMenu("NexUI/Samples/Collection Demo (UI Toolkit)")]
    [RequireComponent(typeof(UIDocument))]
    public sealed class CollectionDemoUIToolkit : MonoBehaviour
    {
        [SerializeField] private int m_ItemCount = 10_000;

        [SerializeField, Tooltip("Grid instead of a single column, to show the same options driving both.")]
        private bool m_Grid;

        private readonly NXCollectionSource<CollectionDemoItem> _source = new NXCollectionSource<CollectionDemoItem>();
        private NXCollectionViewElement _collection;
        private Label _status;
        private List<CollectionDemoItem> _items;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();

            _status = new Label { style = { paddingLeft = 8, paddingTop = 6, paddingBottom = 6 } };
            root.Add(_status);

            _collection = new NXCollectionViewElement
            {
                style = { flexGrow = 1 },
                Options = new NXCollectionOptions
                {
                    Layout = m_Grid ? NXCollectionLayout.Grid : NXCollectionLayout.Vertical,
                    Virtualization = NXVirtualizationMode.FixedSize,
                    Selection = NXSelectionMode.Multiple,
                    Interactions = NXCollectionInteractions.Activate,
                    ItemSize = m_Grid ? 88f : 36f,
                    ItemCrossSize = 88f,
                    AutoColumns = m_Grid,
                    Spacing = 2f,
                    CrossSpacing = 2f
                }
            };

            _collection.MakeItem = () =>
            {
                var view = new Label();
                view.AddToClassList("collection-demo__row");
                view.style.paddingLeft = 8;
                view.style.unityTextAlign = TextAnchor.MiddleLeft;
                view.style.backgroundColor = new Color(0.13f, 0.16f, 0.22f, 1f);
                return view;
            };

            _collection.BindItem = (index, item, view) =>
            {
                var row = item as CollectionDemoItem;
                if (view is Label label) label.text = row == null ? string.Empty : $"{row.Name}  x{row.Amount}";
                view.style.backgroundColor = _collection.Controller.IsSelected(index)
                    ? new Color(0.25f, 0.42f, 0.68f, 1f)
                    : new Color(0.13f, 0.16f, 0.22f, 1f);
            };

            // The state host is where the loading/empty/error view goes. One label covers all three
            // here; a real screen would style them apart through the is-loading/is-empty/is-error
            // classes the element sets.
            var stateLabel = new Label("No items");
            stateLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            stateLabel.style.flexGrow = 1;
            _collection.StateHost.Add(stateLabel);
            _collection.StateHost.style.flexGrow = 1;

            _collection.Controller.SelectionChanged += _ => Report();
            _collection.Controller.VisibleRangeChanged += _ => Report();
            _collection.Controller.StateChanged += state =>
            {
                stateLabel.text = state switch
                {
                    NXCollectionState.Loading => "Loading…",
                    NXCollectionState.Error => "Could not load items",
                    _ => "No items"
                };
                Report();
            };

            _collection.Source = _source;
            root.Add(_collection);

            Load();
        }

        public void Load()
        {
            _collection.State = NXCollectionState.Loading;
            _items = CollectionDemoData.Build(m_ItemCount);
            _source.Set(_items);
            _collection.State = NXCollectionState.Content;
            Report();
        }

        public void Clear() => _source.Set(CollectionDemoData.Build(0));

        private void Report()
        {
            if (_status == null || _collection == null) return;
            var controller = _collection.Controller;
            _status.text = $"{controller.ItemCount} items | realized {controller.VisibleRange} | " +
                           $"{controller.ColumnCount} col | selected {controller.SelectedIndices.Count} | {controller.State}";
        }
    }
}
