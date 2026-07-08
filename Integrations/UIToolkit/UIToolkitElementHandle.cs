using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
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
            Add<IUIPointerCapability>(new VePointer(_element));
            Add<IUIFocusCapability>(new VeFocus(_element));

            switch (_element)
            {
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
                else if (tokenKey.StartsWith("radius.") && float.TryParse(value, out var r))
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
            private Vector2 _position;
            private Vector3 _scale = Vector3.one;
            private float _rotation;

            public VeTransform(VisualElement ve) => _ve = ve;

            public float Opacity
            {
                get => _ve.resolvedStyle.opacity;
                set => _ve.style.opacity = value;
            }

            public Vector2 Position
            {
                get => _position;
                set { _position = value; _ve.style.translate = new Translate(value.x, value.y, 0); }
            }

            public Vector3 Scale
            {
                get => _scale;
                set { _scale = value; _ve.style.scale = new Scale(new Vector2(value.x, value.y)); }
            }

            public float Rotation
            {
                get => _rotation;
                set { _rotation = value; _ve.style.rotate = new Rotate(new Angle(value, AngleUnit.Degree)); }
            }
        }

        private sealed class VeText : IUITextCapability
        {
            private readonly TextElement _text;
            public VeText(TextElement text) => _text = text;
            public string Text { get => _text.text; set => _text.text = value; }
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
