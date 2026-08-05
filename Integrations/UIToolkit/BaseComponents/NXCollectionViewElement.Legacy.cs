#if !UNITY_2023_2_OR_NEWER
using emiteat.NexUI.Components;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    // Unity 2022.3 UXML plumbing for NXCollectionViewElement.
    // See UxmlCompatibility.cs for why this lives in a separate file.

    public partial class NXCollectionViewElement
    {
        public new class UxmlFactory : UxmlFactory<NXCollectionViewElement, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            // These four names are spelled out in the element's own [UxmlAttribute("...")] arguments
            // rather than derived, so they are copied verbatim here instead of kebab-cased.
            private readonly UxmlFloatAttributeDescription _itemSize =
                new UxmlFloatAttributeDescription { name = "item-size" };
            private readonly UxmlIntAttributeDescription _columnCount =
                new UxmlIntAttributeDescription { name = "column-count" };
            private readonly UxmlEnumAttributeDescription<NXCollectionLayout> _layoutMode =
                new UxmlEnumAttributeDescription<NXCollectionLayout> { name = "layout-mode" };
            private readonly UxmlEnumAttributeDescription<NXSelectionMode> _selectionMode =
                new UxmlEnumAttributeDescription<NXSelectionMode> { name = "selection-mode" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXCollectionViewElement)ve;

                // Layout first: itemSize and columnCount are read against the layout the controller
                // is in, and each setter invalidates, so setting them in this order costs one
                // rebuild instead of three.
                target.layoutMode = _layoutMode.GetValueFromBag(bag, context);
                target.selectionMode = _selectionMode.GetValueFromBag(bag, context);
                target.itemSize = _itemSize.GetValueFromBag(bag, context);
                target.columnCount = _columnCount.GetValueFromBag(bag, context);
            }
        }
    }
}
#endif
