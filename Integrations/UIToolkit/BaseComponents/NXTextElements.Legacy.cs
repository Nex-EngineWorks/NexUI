#if !UNITY_2023_2_OR_NEWER
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    // Unity 2022.3 UXML plumbing for the elements in NXTextElements.cs.
    // See UxmlCompatibility.cs for why this lives in a separate file.

    public partial class NXMarqueeLabel
    {
        public new class UxmlFactory : UxmlFactory<NXMarqueeLabel, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _text =
                new UxmlStringAttributeDescription { name = "text" };
            private readonly UxmlFloatAttributeDescription _pixelsPerSecond =
                new UxmlFloatAttributeDescription { name = "pixels-per-second", defaultValue = 40f };
            private readonly UxmlFloatAttributeDescription _pauseSeconds =
                new UxmlFloatAttributeDescription { name = "pause-seconds", defaultValue = 1f };
            private readonly UxmlBoolAttributeDescription _loop =
                new UxmlBoolAttributeDescription { name = "loop" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXMarqueeLabel)ve;
                target.pixelsPerSecond = _pixelsPerSecond.GetValueFromBag(bag, context);
                target.pauseSeconds = _pauseSeconds.GetValueFromBag(bag, context);
                target.loop = _loop.GetValueFromBag(bag, context);
                target.text = _text.GetValueFromBag(bag, context);
            }
        }
    }

    public partial class NXTypewriterLabel
    {
        public new class UxmlFactory : UxmlFactory<NXTypewriterLabel, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _text =
                new UxmlStringAttributeDescription { name = "text" };
            private readonly UxmlFloatAttributeDescription _charactersPerSecond =
                new UxmlFloatAttributeDescription { name = "characters-per-second", defaultValue = 30f };
            private readonly UxmlBoolAttributeDescription _playOnAttach =
                new UxmlBoolAttributeDescription { name = "play-on-attach", defaultValue = true };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXTypewriterLabel)ve;
                target.charactersPerSecond = _charactersPerSecond.GetValueFromBag(bag, context);
                target.playOnAttach = _playOnAttach.GetValueFromBag(bag, context);

                // Assigned last: the setter restarts the reveal, so it has to see the final speed.
                target.text = _text.GetValueFromBag(bag, context);
            }
        }
    }

    public partial class NXNumberTickerLabel
    {
        public new class UxmlFactory : UxmlFactory<NXNumberTickerLabel, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlDoubleAttributeDescription _value =
                new UxmlDoubleAttributeDescription { name = "value" };
            private readonly UxmlFloatAttributeDescription _duration =
                new UxmlFloatAttributeDescription { name = "duration", defaultValue = 0.4f };
            private readonly UxmlStringAttributeDescription _format =
                new UxmlStringAttributeDescription { name = "format", defaultValue = "N0" };
            private readonly UxmlStringAttributeDescription _prefix =
                new UxmlStringAttributeDescription { name = "prefix", defaultValue = "" };
            private readonly UxmlStringAttributeDescription _suffix =
                new UxmlStringAttributeDescription { name = "suffix", defaultValue = "" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                var target = (NXNumberTickerLabel)ve;
                target.duration = _duration.GetValueFromBag(bag, context);
                target.format = _format.GetValueFromBag(bag, context);
                target.prefix = _prefix.GetValueFromBag(bag, context);
                target.suffix = _suffix.GetValueFromBag(bag, context);

                // A value authored in UXML is the starting state, not a change to animate toward.
                target.SetValue(_value.GetValueFromBag(bag, context), animate: false);
            }
        }
    }

    public partial class NXHoldButton
    {
        public new class UxmlFactory : UxmlFactory<NXHoldButton, UxmlTraits> { }

        public new class UxmlTraits : Button.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _holdSeconds =
                new UxmlFloatAttributeDescription { name = "hold-seconds", defaultValue = 1f };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(ve, bag, context);
                ((NXHoldButton)ve).holdSeconds = _holdSeconds.GetValueFromBag(bag, context);
            }
        }
    }
}
#endif
