using System;
using emiteat.NexUI.Compiled;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Builds the control a compiled node asks for, and hands back a way to read its value.
    /// </summary>
    /// <remarks>
    /// Keyed by <see cref="NexNodeProgram.ControlId"/> - the same registry key the uGUI backend
    /// switches on - so the compiled program stays backend-neutral and a screen never says which
    /// engine control it wanted.
    ///
    /// Unlike uGUI, a control here <em>is</em> the element rather than a component added to one.
    /// So this returns the element to use in place of the plain one the builder would have made,
    /// and the builder parents that instead. A key this backend does not know returns null, and the
    /// node is built as a plain element: still laid out, still styled, with the compiler's binding
    /// diagnostic already explaining that the value has nowhere to go.
    /// </remarks>
    public static class NexUIToolkitControls
    {
        /// <summary>
        /// The element a node's control key needs, or null when a plain element will do.
        /// </summary>
        public static VisualElement Create(in NexNodeProgram node,
            out INexValueHandle value, out INexTextHandle text)
        {
            value = null;
            text = null;

            switch (node.ControlId)
            {
                case "Slider":
                case "Scrollbar":
                {
                    var slider = new Slider
                    {
                        lowValue = node.ValueMin,
                        highValue = node.ValueMax > node.ValueMin ? node.ValueMax : node.ValueMin + 1f
                    };
                    value = new FieldValueHandle<float>(slider, v => v, v => v);
                    return slider;
                }

                case "Toggle":
                {
                    var toggle = new Toggle();
                    // A toggle's value is a bool and the binding vocabulary is a float, so it is
                    // carried as 0/1 - the same convention the uGUI backend uses, which is what
                    // lets one authored binding drive either backend.
                    value = new FieldValueHandle<bool>(toggle, v => v ? 1f : 0f, v => v >= 0.5f);
                    return toggle;
                }

                case "Dropdown":
                case "DropdownTMP":
                {
                    var dropdown = new DropdownField();
                    value = new DropdownValueHandle(dropdown);
                    return dropdown;
                }

                case "InputField":
                case "InputFieldTMP":
                {
                    var field = new TextField();
                    text = new TextFieldHandle(field);
                    return field;
                }

                case "ProgressBar":
                case "StatBar":
                {
                    var bar = new ProgressBar
                    {
                        lowValue = node.ValueMin,
                        highValue = node.ValueMax > node.ValueMin ? node.ValueMax : node.ValueMin + 1f
                    };
                    value = new ProgressValueHandle(bar);
                    return bar;
                }

                case "ScrollView":
                    return new ScrollView();

                case "ButtonTMP":
                case "Button":
                    return new Button();

                default:
                    return null;
            }
        }

        /// <summary>
        /// A value handle over any <see cref="BaseField{T}"/>, converting to and from the float the
        /// binding vocabulary speaks.
        /// </summary>
        /// <remarks>
        /// One generic handle rather than one per control: every UI Toolkit field raises the same
        /// <c>ChangeEvent</c> and exposes the same <c>value</c>, so the only thing that differs is
        /// the conversion, which is what the two delegates are.
        /// </remarks>
        private sealed class FieldValueHandle<T> : INexValueHandle
        {
            private BaseField<T> _field;
            private readonly Func<T, float> _toFloat;
            private readonly Func<float, T> _fromFloat;
            private bool _suppress;

            public event Action<float> UserChanged;

            public FieldValueHandle(BaseField<T> field, Func<T, float> toFloat, Func<float, T> fromFloat)
            {
                _field = field;
                _toFloat = toFloat;
                _fromFloat = fromFloat;
                _field.RegisterValueChangedCallback(OnChanged);
            }

            public float Value
            {
                get => _field != null ? _toFloat(_field.value) : 0f;
                set
                {
                    if (_field == null) return;

                    // A binding writing the value must not read back as a user edit, or a two-way
                    // binding feeds itself in a loop for as long as the screen is open.
                    _suppress = true;
                    _field.SetValueWithoutNotify(_fromFloat(value));
                    _suppress = false;
                }
            }

            private void OnChanged(ChangeEvent<T> evt)
            {
                if (_suppress) return;
                UserChanged?.Invoke(_toFloat(evt.newValue));
            }

            public void Dispose()
            {
                _field?.UnregisterValueChangedCallback(OnChanged);
                _field = null;
                UserChanged = null;
            }
        }

        /// <summary>
        /// A dropdown's value is its selected index, which is what a binding carries.
        /// </summary>
        /// <remarks>
        /// Not its text: an index survives a language change and a text value does not, and the
        /// uGUI backend already binds the index.
        /// </remarks>
        private sealed class DropdownValueHandle : INexValueHandle
        {
            private DropdownField _field;
            private bool _suppress;

            public event Action<float> UserChanged;

            public DropdownValueHandle(DropdownField field)
            {
                _field = field;
                _field.RegisterValueChangedCallback(OnChanged);
            }

            public float Value
            {
                get => _field != null ? _field.index : 0f;
                set
                {
                    if (_field == null) return;
                    _suppress = true;
                    _field.index = UnityEngine.Mathf.RoundToInt(value);
                    _suppress = false;
                }
            }

            private void OnChanged(ChangeEvent<string> evt)
            {
                if (_suppress) return;
                UserChanged?.Invoke(_field != null ? _field.index : 0f);
            }

            public void Dispose()
            {
                _field?.UnregisterValueChangedCallback(OnChanged);
                _field = null;
                UserChanged = null;
            }
        }

        /// <summary>
        /// A read-only fill. Never raises <see cref="UserChanged"/> - it shows a number and never
        /// collects one - but implements the same interface as the controls that do.
        /// </summary>
        private sealed class ProgressValueHandle : INexValueHandle
        {
            private ProgressBar _bar;

            public event Action<float> UserChanged;

            public ProgressValueHandle(ProgressBar bar) => _bar = bar;

            public float Value
            {
                get => _bar != null ? _bar.value : 0f;
                set { if (_bar != null) _bar.value = value; }
            }

            public void Dispose()
            {
                _bar = null;
                UserChanged = null;
            }
        }

        private sealed class TextFieldHandle : INexTextHandle
        {
            private TextField _field;
            private bool _suppress;

            public event Action<string> UserChanged;

            public TextFieldHandle(TextField field)
            {
                _field = field;
                _field.RegisterValueChangedCallback(OnChanged);
            }

            public string Text
            {
                get => _field != null ? _field.value ?? string.Empty : string.Empty;
                set
                {
                    if (_field == null) return;

                    var next = value ?? string.Empty;

                    // Skipped when unchanged, not just suppressed. Assigning the same text still
                    // moves the caret to the end, so a binding echoing back what was just typed
                    // would fight the user for the cursor on every keystroke.
                    if (string.Equals(_field.value, next, StringComparison.Ordinal)) return;

                    _suppress = true;
                    _field.SetValueWithoutNotify(next);
                    _suppress = false;
                }
            }

            private void OnChanged(ChangeEvent<string> evt)
            {
                if (_suppress) return;
                UserChanged?.Invoke(evt.newValue ?? string.Empty);
            }

            public void Dispose()
            {
                _field?.UnregisterValueChangedCallback(OnChanged);
                _field = null;
                UserChanged = null;
            }
        }
    }
}
