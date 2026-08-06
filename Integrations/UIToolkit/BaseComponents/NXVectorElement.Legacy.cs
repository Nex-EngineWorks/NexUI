#if !UNITY_2023_2_OR_NEWER
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    // Unity 2022.3 UXML plumbing for NXVectorElement.cs. On 2023.2+ the [UxmlElement] source
    // generator produces the equivalent and this file compiles to nothing.
    //
    // Attribute names match what the generator derives from the property names on Unity 6
    // (camelCase -> kebab-case), so one .uxml loads unchanged on both editors.

    public partial class NXVectorElement
    {
        public new class UxmlFactory : UxmlFactory<NXVectorElement, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _pathData =
                new UxmlStringAttributeDescription { name = "path-data", defaultValue = string.Empty };
            private readonly UxmlColorAttributeDescription _fillColor =
                new UxmlColorAttributeDescription { name = "fill-color", defaultValue = Color.white };
            private readonly UxmlBoolAttributeDescription _filled =
                new UxmlBoolAttributeDescription { name = "filled", defaultValue = true };
            private readonly UxmlFloatAttributeDescription _strokeWidth =
                new UxmlFloatAttributeDescription { name = "stroke-width", defaultValue = 0f };
            private readonly UxmlColorAttributeDescription _strokeColor =
                new UxmlColorAttributeDescription { name = "stroke-color", defaultValue = Color.black };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXVectorElement)ve;

                // Path first: it replaces the shape object, and the appearance setters below then
                // write onto the one that ends up being drawn.
                target.pathData = _pathData.GetValueFromBag(bag, context);
                target.filled = _filled.GetValueFromBag(bag, context);
                target.fillColor = _fillColor.GetValueFromBag(bag, context);
                target.strokeWidth = _strokeWidth.GetValueFromBag(bag, context);
                target.strokeColor = _strokeColor.GetValueFromBag(bag, context);
                target.MarkDirtyRepaint();
            }
        }
    }
}
#endif
