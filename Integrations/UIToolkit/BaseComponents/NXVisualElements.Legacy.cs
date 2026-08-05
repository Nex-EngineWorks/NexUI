#if !UNITY_2023_2_OR_NEWER
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    // Unity 2022.3 UXML plumbing for the elements in NXVisualElements.cs. On 2023.2+ the
    // [UxmlElement] source generator produces the equivalent and this file compiles to nothing.
    //
    // Attribute names match what the generator derives from the property names on Unity 6
    // (camelCase -> kebab-case), so one .uxml loads unchanged on both editors.

    public partial class NXGradientElement
    {
        public new class UxmlFactory : UxmlFactory<NXGradientElement, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlColorAttributeDescription _startColor =
                new UxmlColorAttributeDescription { name = "start-color", defaultValue = Color.white };
            private readonly UxmlColorAttributeDescription _endColor =
                new UxmlColorAttributeDescription { name = "end-color", defaultValue = new Color(0.6f, 0.6f, 0.6f, 1f) };
            private readonly UxmlFloatAttributeDescription _angle =
                new UxmlFloatAttributeDescription { name = "angle", defaultValue = 90f };
            private readonly UxmlIntAttributeDescription _steps =
                new UxmlIntAttributeDescription { name = "steps", defaultValue = 24 };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXGradientElement)ve;
                target.startColor = _startColor.GetValueFromBag(bag, context);
                target.endColor = _endColor.GetValueFromBag(bag, context);
                target.angle = _angle.GetValueFromBag(bag, context);
                target.steps = _steps.GetValueFromBag(bag, context);
                target.MarkDirtyRepaint();
            }
        }
    }

    public partial class NXSafeAreaElement
    {
        public new class UxmlFactory : UxmlFactory<NXSafeAreaElement, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlBoolAttributeDescription _left =
                new UxmlBoolAttributeDescription { name = "apply-left", defaultValue = true };
            private readonly UxmlBoolAttributeDescription _right =
                new UxmlBoolAttributeDescription { name = "apply-right", defaultValue = true };
            private readonly UxmlBoolAttributeDescription _top =
                new UxmlBoolAttributeDescription { name = "apply-top", defaultValue = true };
            private readonly UxmlBoolAttributeDescription _bottom =
                new UxmlBoolAttributeDescription { name = "apply-bottom", defaultValue = true };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXSafeAreaElement)ve;
                target.applyLeft = _left.GetValueFromBag(bag, context);
                target.applyRight = _right.GetValueFromBag(bag, context);
                target.applyTop = _top.GetValueFromBag(bag, context);
                target.applyBottom = _bottom.GetValueFromBag(bag, context);
                target.Apply();
            }
        }
    }

    public partial class NXRadialContainer
    {
        public new class UxmlFactory : UxmlFactory<NXRadialContainer, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _radius =
                new UxmlFloatAttributeDescription { name = "radius", defaultValue = 120f };
            private readonly UxmlFloatAttributeDescription _startAngle =
                new UxmlFloatAttributeDescription { name = "start-angle", defaultValue = 90f };
            private readonly UxmlFloatAttributeDescription _sweepAngle =
                new UxmlFloatAttributeDescription { name = "sweep-angle", defaultValue = 360f };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXRadialContainer)ve;
                target.radius = _radius.GetValueFromBag(bag, context);
                target.startAngle = _startAngle.GetValueFromBag(bag, context);
                target.sweepAngle = _sweepAngle.GetValueFromBag(bag, context);
                target.Arrange();
            }
        }
    }

    public partial class NXSegmentedBarElement
    {
        public new class UxmlFactory : UxmlFactory<NXSegmentedBarElement, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlIntAttributeDescription _segments =
                new UxmlIntAttributeDescription { name = "segments", defaultValue = 5 };
            private readonly UxmlFloatAttributeDescription _value =
                new UxmlFloatAttributeDescription { name = "value", defaultValue = 1f };
            private readonly UxmlFloatAttributeDescription _gap =
                new UxmlFloatAttributeDescription { name = "gap", defaultValue = 4f };
            private readonly UxmlColorAttributeDescription _fillColor =
                new UxmlColorAttributeDescription { name = "fill-color", defaultValue = new Color(0.25f, 0.55f, 0.95f) };
            private readonly UxmlColorAttributeDescription _emptyColor =
                new UxmlColorAttributeDescription { name = "empty-color", defaultValue = new Color(1f, 1f, 1f, 0.15f) };
            private readonly UxmlBoolAttributeDescription _vertical =
                new UxmlBoolAttributeDescription { name = "vertical" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXSegmentedBarElement)ve;
                target.segments = _segments.GetValueFromBag(bag, context);
                target.value = _value.GetValueFromBag(bag, context);
                target.gap = _gap.GetValueFromBag(bag, context);
                target.fillColor = _fillColor.GetValueFromBag(bag, context);
                target.emptyColor = _emptyColor.GetValueFromBag(bag, context);
                target.vertical = _vertical.GetValueFromBag(bag, context);
                target.MarkDirtyRepaint();
            }
        }
    }

    public partial class NXCooldownElement
    {
        public new class UxmlFactory : UxmlFactory<NXCooldownElement, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _remaining =
                new UxmlFloatAttributeDescription { name = "remaining" };
            private readonly UxmlColorAttributeDescription _overlayColor =
                new UxmlColorAttributeDescription { name = "overlay-color", defaultValue = new Color(0f, 0f, 0f, 0.55f) };
            private readonly UxmlBoolAttributeDescription _clockwise =
                new UxmlBoolAttributeDescription { name = "clockwise", defaultValue = true };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXCooldownElement)ve;
                target.remaining = _remaining.GetValueFromBag(bag, context);
                target.overlayColor = _overlayColor.GetValueFromBag(bag, context);
                target.clockwise = _clockwise.GetValueFromBag(bag, context);
                target.MarkDirtyRepaint();
            }
        }
    }
}
#endif
