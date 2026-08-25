using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Wraps a <see cref="VisualElement"/> as a backend-independent
    /// <see cref="IUIElementHandle"/>, exposing capabilities appropriate to the element
    /// type. This is the single place UI Toolkit types are touched on the element side.
    /// </summary>
    public sealed class UIToolkitElementHandle : IUIElementHandle
    {
        private readonly VisualElement _element;
        private readonly Dictionary<Type, object> _capabilities = new Dictionary<Type, object>();

        public string Id { get; }
        public UIRenderBackend Backend => UIRenderBackend.UIToolkit;
        public object Native => _element;

        public UIToolkitElementHandle(VisualElement element, string id = null)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            Id = id ?? element.name;
            BuildCapabilities();
        }

        public bool Has<TCapability>() where TCapability : class
            => _capabilities.ContainsKey(typeof(TCapability));

        public TCapability As<TCapability>() where TCapability : class
            => _capabilities.TryGetValue(typeof(TCapability), out var cap) ? cap as TCapability : null;

        private void Add<TCapability>(TCapability cap) where TCapability : class
            => _capabilities[typeof(TCapability)] = cap;

        private void BuildCapabilities()
        {
            // Common to every VisualElement.
            Add<IUIVisibilityCapability>(new VeVisibility(_element));
            Add<IUIStyleCapability>(new VeStyle(_element));
            Add<IUITransformCapability>(new VeTransform(_element));
            Add<IUISizeCapability>(new VeSize(_element));
            Add<IUIPointerCapability>(new VePointer(_element));
            Add<IUIFocusCapability>(new VeFocus(_element));
            Add<IUIColorCapability>(new VeColor(_element));
            Add<IUITypographyCapability>(new VeTypography(_element));

            switch (_element)
            {
                case TextField textField:
                    var textInput = new VeTextInput(textField);
                    Add<IUITextCapability>(textInput);
                    Add<IUITextInputCapability>(textInput);
                    Add<IUIInteractableCapability>(new VeInteractable(textField));
                    break;
                case Slider slider:
                    var sliderValue = new VeSliderValue(slider);
                    Add<IUIValueCapability>(sliderValue);
                    Add<IUIValueInputCapability>(sliderValue);
                    Add<IUIInteractableCapability>(new VeInteractable(slider));
                    break;
                case SliderInt sliderInt:
                    var sliderIntValue = new VeSliderIntValue(sliderInt);
                    Add<IUIValueCapability>(sliderIntValue);
                    Add<IUIValueInputCapability>(sliderIntValue);
                    Add<IUIInteractableCapability>(new VeInteractable(sliderInt));
                    break;
                case Button button:
                    Add<IUITextCapability>(new VeText(button));
                    Add<IUIClickCapability>(new VeClick(button));
                    Add<IUIInteractableCapability>(new VeInteractable(button));
                    break;
                case Label label:
                    Add<IUITextCapability>(new VeText(label));
                    break;
                case ProgressBar bar:
                    Add<IUIValueCapability>(new VeValue(bar));
                    break;
            }
        }

        // ---- Capability adapters -------------------------------------------

        private sealed class VeVisibility : IUIVisibilityCapability
        {
            private readonly VisualElement _ve;
            public VeVisibility(VisualElement ve) => _ve = ve;
            public bool Visible
            {
                get => _ve.style.display != DisplayStyle.None;
                set => _ve.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private sealed class VeStyle : IUIStyleCapability
        {
            private readonly VisualElement _ve;
            public VeStyle(VisualElement ve) => _ve = ve;

            public void SetClass(string className, bool on)
            {
                if (string.IsNullOrEmpty(className)) return;
                if (on) _ve.AddToClassList(className);
                else _ve.RemoveFromClassList(className);
            }

            public void ApplyToken(string tokenKey, string value)
            {
                // Minimal inline mapping for common tokens; a StyleSheet-based applier
                // can supersede this later.
                if (string.IsNullOrEmpty(tokenKey) || value == null) return;

                if (tokenKey.StartsWith("color.") && ColorUtility.TryParseHtmlString(value, out var c))
                {
                    if (tokenKey == "color.text") _ve.style.color = c;
                    else _ve.style.backgroundColor = c;
                }
                else if (tokenKey.StartsWith("radius.") &&
                         float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
                {
                    _ve.style.borderTopLeftRadius = r;
                    _ve.style.borderTopRightRadius = r;
                    _ve.style.borderBottomLeftRadius = r;
                    _ve.style.borderBottomRightRadius = r;
                }
            }
        }

        private sealed class VeTransform : IUITransformCapability
        {
            private readonly VisualElement _ve;

            public VeTransform(VisualElement ve) => _ve = ve;

            public float Opacity
            {
                // Prefer an explicit inline opacity over the resolved one: a just-written value must
                // read back immediately inside an animation loop, not one layout pass later.
                get => _ve.style.opacity.keyword != StyleKeyword.Null
                    ? _ve.style.opacity.value
                    : _ve.resolvedStyle.opacity;
                set => _ve.style.opacity = value;
            }

            public Vector2 Position
            {
                get
                {
                    var t = _ve.style.translate.keyword == StyleKeyword.Null
                        ? _ve.resolvedStyle.translate
                        : _ve.style.translate.value;
                    return new Vector2(t.x.value, t.y.value);
                }
                set => _ve.style.translate = new Translate(value.x, value.y, 0);
            }

            // Scale keeps the last-written value: the Scale style struct's accessor surface varies
            // across Unity versions, and scale is the one axis animations always write before read.
            private Vector3 _lastWrittenScale = Vector3.one;

            public Vector3 Scale
            {
                get => _lastWrittenScale;
                set
                {
                    _lastWrittenScale = value;
                    _ve.style.scale = new Scale(new Vector2(value.x, value.y));
                }
            }

            public float Rotation
            {
                get
                {
                    var r = _ve.style.rotate.keyword == StyleKeyword.Null
                        ? _ve.resolvedStyle.rotate
                        : _ve.style.rotate.value;
                    var angle = r.angle;
                    return angle.unit == AngleUnit.Degree ? angle.value : angle.value * Mathf.Rad2Deg;
                }
                set => _ve.style.rotate = new Rotate(new Angle(value, AngleUnit.Degree));
            }
        }

        private sealed class VeSize : IUISizeCapability
        {
            private readonly VisualElement _ve;
            public VeSize(VisualElement ve) => _ve = ve;
            public Vector2 SizeDelta
            {
                get => new Vector2(_ve.resolvedStyle.width, _ve.resolvedStyle.height);
                set { _ve.style.width = value.x; _ve.style.height = value.y; }
            }
        }

        private sealed class VeColor : IUIColorCapability
        {
            private readonly VisualElement _ve;
            public VeColor(VisualElement ve) => _ve = ve;
            public Color BackgroundColor { get => _ve.resolvedStyle.backgroundColor; set => _ve.style.backgroundColor = value; }
            public Color TextColor { get => _ve.resolvedStyle.color; set => _ve.style.color = value; }
        }

        private sealed class VeTypography : IUITypographyCapability
        {
            private readonly VisualElement _ve;
            public VeTypography(VisualElement ve) => _ve = ve;
            public float FontSize { get => _ve.resolvedStyle.fontSize; set => _ve.style.fontSize = value; }
        }

        private sealed class VeText : IUITextCapability
        {
            private readonly TextElement _text;
            public VeText(TextElement text) => _text = text;
            public string Text { get => _text.text; set => _text.text = value; }
        }

        private sealed class VeTextInput : IUITextInputCapability
        {
            private readonly TextField _field;
            public string Text { get => _field.value; set => _field.SetValueWithoutNotify(value ?? string.Empty); }
            public event Action<string> TextChanged;

            public void OnChanged(ChangeEvent<string> evt) => TextChanged?.Invoke(evt.newValue);

            public VeTextInput(TextField field)
            {
                _field = field;
                field.RegisterValueChangedCallback(OnChanged);
            }
        }

        private sealed class VeClick : IUIClickCapability
        {
            private readonly Button _button;
            public VeClick(Button button) => _button = button;
            public event Action Clicked
            {
                add => _button.clicked += value;
                remove => _button.clicked -= value;
            }
        }

        private sealed class VeInteractable : IUIInteractableCapability
        {
            private readonly VisualElement _ve;
            public VeInteractable(VisualElement ve) => _ve = ve;
            public bool Interactable
            {
                get => _ve.enabledSelf;
                set => _ve.SetEnabled(value);
            }
        }

        private sealed class VeValue : IUIValueCapability
        {
            private readonly ProgressBar _bar;
            public VeValue(ProgressBar bar) => _bar = bar;
            public float Value { get => _bar.value; set => _bar.value = value; }
            public float Min { get => _bar.lowValue; set => _bar.lowValue = value; }
            public float Max { get => _bar.highValue; set => _bar.highValue = value; }
        }

        private sealed class VeSliderValue : IUIValueInputCapability
        {
            private readonly Slider _slider;
            public VeSliderValue(Slider slider)
            {
                _slider = slider;
                slider.RegisterValueChangedCallback(evt => ValueChanged?.Invoke(evt.newValue));
            }
            public float Value { get => _slider.value; set => _slider.SetValueWithoutNotify(value); }
            public float Min { get => _slider.lowValue; set => _slider.lowValue = value; }
            public float Max { get => _slider.highValue; set => _slider.highValue = value; }
            public event Action<float> ValueChanged;
        }

        private sealed class VeSliderIntValue : IUIValueInputCapability
        {
            private readonly SliderInt _slider;
            public VeSliderIntValue(SliderInt slider)
            {
                _slider = slider;
                slider.RegisterValueChangedCallback(evt => ValueChanged?.Invoke(evt.newValue));
            }
            public float Value { get => _slider.value; set => _slider.SetValueWithoutNotify(Mathf.RoundToInt(value)); }
            public float Min { get => _slider.lowValue; set => _slider.lowValue = Mathf.RoundToInt(value); }
            public float Max { get => _slider.highValue; set => _slider.highValue = Mathf.RoundToInt(value); }
            public event Action<float> ValueChanged;
        }

        private sealed class VePointer : IUIPointerCapability
        {
            public event Action PointerEntered;
            public event Action PointerExited;
            public event Action PointerDown;
            public event Action PointerUp;

            public VePointer(VisualElement ve)
            {
                ve.RegisterCallback<PointerEnterEvent>(_ => PointerEntered?.Invoke());
                ve.RegisterCallback<PointerLeaveEvent>(_ => PointerExited?.Invoke());
                ve.RegisterCallback<PointerDownEvent>(_ => PointerDown?.Invoke());
                ve.RegisterCallback<PointerUpEvent>(_ => PointerUp?.Invoke());
            }
        }

        private sealed class VeFocus : IUIFocusCapability
        {
            private readonly VisualElement _ve;
            public event Action Focused;
            public event Action Blurred;

            public VeFocus(VisualElement ve)
            {
                _ve = ve;
                ve.RegisterCallback<FocusInEvent>(_ => Focused?.Invoke());
                ve.RegisterCallback<FocusOutEvent>(_ => Blurred?.Invoke());
            }

            public bool HasFocus => _ve.focusController?.focusedElement == _ve;
        }
    }
}
