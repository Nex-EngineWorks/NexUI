using System;
using emiteat.NexUI.Compiled;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Attaches the control a compiled node asks for, and hands back a way to read its value.
    /// </summary>
    /// <remarks>
    /// Keyed by <see cref="NexNodeProgram.ControlId"/>, which is a registry key rather than a Unity
    /// type, so the compiled program stays backend-neutral. A key this backend does not know
    /// produces no control and no exception: the node is still built and still laid out, and the
    /// compiler has already reported that the binding will not reach anything.
    /// </remarks>
    public static class NexUGuiControls
    {
        public static INexValueHandle Attach(GameObject target, in NexNodeProgram node)
        {
            if (target == null || string.IsNullOrEmpty(node.ControlId)) return null;

            switch (node.ControlId)
            {
                case "Slider": return SliderHandle.Create(target, node);
                case "Scrollbar": return ScrollbarHandle.Create(target);
                case "Toggle": return ToggleHandle.Create(target);
                case "Dropdown":
                case "DropdownTMP": return DropdownHandle.Create(target);
                case "ProgressBar":
                case "StatBar": return FillHandle.Create(target, node, radial: false);
                case "RadialFill": return FillHandle.Create(target, node, radial: true);
                default: return null;
            }
        }

        /// <summary>
        /// Attaches the text control a compiled node asks for, when it has one.
        /// </summary>
        /// <remarks>
        /// Returns null for every node that is not an input field, including labels: a label is
        /// written to directly and has no user edit to report back, so giving it a handle would
        /// only add a subscription that never fires.
        /// </remarks>
        public static INexTextHandle AttachText(GameObject target, in NexNodeProgram node)
        {
            if (target == null || string.IsNullOrEmpty(node.ControlId)) return null;

            switch (node.ControlId)
            {
                case "InputField":
                case "InputFieldTMP": return InputFieldHandle.Create(target);
                default: return null;
            }
        }

        /// <summary>
        /// A slider, with the interaction parts uGUI requires to actually work.
        /// </summary>
        /// <remarks>
        /// uGUI's Slider does nothing without a fill or handle rect - it accepts the drag and has
        /// nowhere to show the result. Building them here rather than expecting the author to add
        /// child elements is what makes "drop a slider on the screen" produce a working slider.
        /// </remarks>
        private sealed class SliderHandle : INexValueHandle
        {
            private Slider _slider;
            private bool _suppress;

            public event Action<float> UserChanged;

            public static SliderHandle Create(GameObject target, in NexNodeProgram node)
            {
                var slider = target.GetComponent<Slider>();
                if (slider == null)
                {
                    var fill = NewChild(target, "Fill", new Color(0.25f, 0.55f, 0.95f));
                    var handleRect = NewChild(target, "Handle", Color.white);

                    // Tagged with the authoring part ids so a nudge the author made in Studio can
                    // find them here. The registry's paths name Unity's stock slider, which this
                    // control deliberately is not.
                    NexPartTag.Mark(fill, "fill");
                    NexPartTag.Mark(handleRect, "handle");

                    slider = target.AddComponent<Slider>();
                    slider.fillRect = fill;
                    slider.handleRect = handleRect;
                    slider.targetGraphic = handleRect.GetComponent<Graphic>();
                }

                slider.minValue = node.ValueMin;
                slider.maxValue = node.ValueMax > node.ValueMin ? node.ValueMax : node.ValueMin + 1f;

                var handle = new SliderHandle { _slider = slider };
                slider.onValueChanged.AddListener(handle.OnChanged);
                return handle;
            }

            private static RectTransform NewChild(GameObject parent, string name, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent.transform, worldPositionStays: false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                go.GetComponent<Image>().color = color;
                return rect;
            }

            public float Value
            {
                get => _slider != null ? _slider.value : 0f;
                set
                {
                    if (_slider == null) return;

                    // A binding writing the value must not read back as a user edit, or a two-way
                    // binding feeds itself in a loop for as long as the screen is open.
                    _suppress = true;
                    _slider.value = value;
                    _suppress = false;
                }
            }

            private void OnChanged(float value)
            {
                if (_suppress) return;
                UserChanged?.Invoke(value);
            }

            public void Dispose()
            {
                if (_slider != null) _slider.onValueChanged.RemoveListener(OnChanged);
                _slider = null;
                UserChanged = null;
            }
        }

        private sealed class ScrollbarHandle : INexValueHandle
        {
            private Scrollbar _scrollbar;
            private bool _suppress;

            public event Action<float> UserChanged;

            public static ScrollbarHandle Create(GameObject target)
            {
                var scrollbar = target.GetComponent<Scrollbar>() ?? target.AddComponent<Scrollbar>();
                var handle = new ScrollbarHandle { _scrollbar = scrollbar };
                scrollbar.onValueChanged.AddListener(handle.OnChanged);
                return handle;
            }

            public float Value
            {
                get => _scrollbar != null ? _scrollbar.value : 0f;
                set
                {
                    if (_scrollbar == null) return;
                    _suppress = true;
                    _scrollbar.value = value;
                    _suppress = false;
                }
            }

            private void OnChanged(float value)
            {
                if (_suppress) return;
                UserChanged?.Invoke(value);
            }

            public void Dispose()
            {
                if (_scrollbar != null) _scrollbar.onValueChanged.RemoveListener(OnChanged);
                _scrollbar = null;
                UserChanged = null;
            }
        }

        /// <summary>A toggle, reported as 0 or 1 so one binding path covers every value control.</summary>
        private sealed class ToggleHandle : INexValueHandle
        {
            private Toggle _toggle;
            private bool _suppress;

            public event Action<float> UserChanged;

            public static ToggleHandle Create(GameObject target)
            {
                var toggle = target.GetComponent<Toggle>();
                if (toggle == null)
                {
                    toggle = target.AddComponent<Toggle>();
                    var graphic = target.GetComponent<Graphic>();
                    if (graphic != null) toggle.targetGraphic = graphic;
                }

                var handle = new ToggleHandle { _toggle = toggle };
                toggle.onValueChanged.AddListener(handle.OnChanged);
                return handle;
            }

            public float Value
            {
                get => _toggle != null && _toggle.isOn ? 1f : 0f;
                set
                {
                    if (_toggle == null) return;
                    _suppress = true;
                    _toggle.isOn = value != 0f;
                    _suppress = false;
                }
            }

            private void OnChanged(bool on)
            {
                if (_suppress) return;
                UserChanged?.Invoke(on ? 1f : 0f);
            }

            public void Dispose()
            {
                if (_toggle != null) _toggle.onValueChanged.RemoveListener(OnChanged);
                _toggle = null;
                UserChanged = null;
            }
        }

        /// <summary>
        /// A dropdown, whose value is the selected index.
        /// </summary>
        /// <remarks>
        /// The index rather than the option text, so the same numeric binding path covers this as
        /// covers a slider. Binding the text instead would mean a value that changes meaning when
        /// the option list is edited or localised - the index at least stays a stable reference
        /// into whatever list the screen currently carries.
        ///
        /// Both dropdown types are handled here. Which one an element got depends on whether the
        /// project uses TextMeshPro, and a binding must not care: the compiler emits
        /// <c>Dropdown</c> or <c>DropdownTMP</c> as the control id, and both arrive as a number.
        /// </remarks>
        private sealed class DropdownHandle : INexValueHandle
        {
            private Dropdown _legacy;
            private TMP_Dropdown _tmp;
            private bool _suppress;

            public event Action<float> UserChanged;

            public static DropdownHandle Create(GameObject target)
            {
                // Whichever is already there wins. A prefab-loaded screen already carries Unity's
                // own dropdown from UGUIControlFactory; a second one here would leave two controls
                // fighting over the same rect.
                var tmp = target.GetComponent<TMP_Dropdown>();
                if (tmp == null && target.GetComponent<Dropdown>() == null) tmp = Build(target);

                if (tmp != null)
                {
                    var tmpHandle = new DropdownHandle { _tmp = tmp };
                    tmp.onValueChanged.AddListener(tmpHandle.OnChanged);
                    return tmpHandle;
                }

                var legacy = target.GetComponent<Dropdown>();
                if (legacy == null) return null;

                var handle = new DropdownHandle { _legacy = legacy };
                legacy.onValueChanged.AddListener(handle.OnChanged);
                return handle;
            }

            /// <summary>
            /// Builds the parts uGUI requires for a dropdown to work.
            /// </summary>
            /// <remarks>
            /// The template is the part that cannot be skipped: a dropdown with none shows its
            /// caption, accepts the click, and never opens - which looks like a broken control
            /// rather than a missing one. uGUI clones the template at runtime and expects a
            /// specific shape inside it (a scroll rect over a viewport over content over one item),
            /// so all of it is built here even though none of it is visible until the list opens.
            ///
            /// Only reached on the compiled path; prefab screens keep Unity's richer control.
            /// </remarks>
            private static TMP_Dropdown Build(GameObject target)
            {
                if (target.GetComponent<Graphic>() == null)
                {
                    var background = target.AddComponent<Image>();
                    background.color = Color.white;
                }

                var captionGo = new GameObject("Label", typeof(RectTransform));
                var captionRect = (RectTransform)captionGo.transform;
                captionRect.SetParent(target.transform, worldPositionStays: false);
                Stretch(captionRect);
                captionRect.offsetMin = new Vector2(8f, 2f);
                captionRect.offsetMax = new Vector2(-20f, -2f);

                NexPartTag.Mark(captionRect, "label");

                var caption = captionGo.AddComponent<TextMeshProUGUI>();
                caption.fontSize = 14f;
                caption.color = Color.black;
                caption.alignment = TextAlignmentOptions.MidlineLeft;
                caption.raycastTarget = false;

                var template = BuildTemplate(target, out var itemLabel, out var itemToggle);

                var dropdown = target.AddComponent<TMP_Dropdown>();
                dropdown.targetGraphic = target.GetComponent<Graphic>();
                dropdown.template = template;
                dropdown.captionText = caption;
                dropdown.itemText = itemLabel;

                // Disabled last. uGUI requires the template to be inactive - it clones it on open -
                // and setting it while the object is live leaves a stray list on screen.
                itemToggle.gameObject.SetActive(true);
                template.gameObject.SetActive(false);
                return dropdown;
            }

            private static RectTransform BuildTemplate(GameObject target,
                out TextMeshProUGUI itemLabel, out Toggle itemToggle)
            {
                var templateGo = new GameObject("Template", typeof(RectTransform), typeof(Image));
                var template = (RectTransform)templateGo.transform;
                template.SetParent(target.transform, worldPositionStays: false);

                // Anchored under the control and growing downward, which is where a list is
                // expected to appear.
                template.anchorMin = new Vector2(0f, 0f);
                template.anchorMax = new Vector2(1f, 0f);
                template.pivot = new Vector2(0.5f, 1f);
                template.anchoredPosition = Vector2.zero;
                template.sizeDelta = new Vector2(0f, 120f);
                templateGo.GetComponent<Image>().color = Color.white;
                NexPartTag.Mark(template, "template");

                var viewport = NewChild(templateGo, "Viewport");
                viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
                viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

                var content = new GameObject("Content", typeof(RectTransform));
                var contentRect = (RectTransform)content.transform;
                contentRect.SetParent(viewport, worldPositionStays: false);
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.sizeDelta = new Vector2(0f, 28f);

                var item = new GameObject("Item", typeof(RectTransform));
                var itemRect = (RectTransform)item.transform;
                itemRect.SetParent(contentRect, worldPositionStays: false);
                itemRect.anchorMin = new Vector2(0f, 0.5f);
                itemRect.anchorMax = new Vector2(1f, 0.5f);
                itemRect.sizeDelta = new Vector2(0f, 24f);

                var itemBackground = item.AddComponent<Image>();
                itemBackground.color = new Color(0.85f, 0.9f, 1f);
                itemToggle = item.AddComponent<Toggle>();
                itemToggle.targetGraphic = itemBackground;

                var labelGo = new GameObject("Item Label", typeof(RectTransform));
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.SetParent(itemRect, worldPositionStays: false);
                Stretch(labelRect);
                labelRect.offsetMin = new Vector2(8f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);

                itemLabel = labelGo.AddComponent<TextMeshProUGUI>();
                itemLabel.fontSize = 14f;
                itemLabel.color = Color.black;
                itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
                itemLabel.raycastTarget = false;

                var scroll = templateGo.AddComponent<ScrollRect>();
                scroll.content = contentRect;
                scroll.viewport = viewport;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;

                return template;
            }

            private static RectTransform NewChild(GameObject parent, string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent.transform, worldPositionStays: false);
                Stretch(rect);
                return rect;
            }

            private static void Stretch(RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            private int OptionCount => _tmp != null ? _tmp.options.Count
                : _legacy != null ? _legacy.options.Count : 0;

            public float Value
            {
                get => _tmp != null ? _tmp.value : _legacy != null ? _legacy.value : 0f;
                set
                {
                    var count = OptionCount;
                    if (count == 0) return;

                    // Clamped: a binding can hold an index from a longer list than this screen
                    // currently shows, and uGUI would otherwise silently snap it to zero - which
                    // reads as "the selection was reset" rather than "that option is gone".
                    var index = Mathf.Clamp(Mathf.RoundToInt(value), 0, count - 1);

                    _suppress = true;
                    if (_tmp != null) _tmp.value = index;
                    else if (_legacy != null) _legacy.value = index;
                    _suppress = false;
                }
            }

            private void OnChanged(int index)
            {
                if (_suppress) return;
                UserChanged?.Invoke(index);
            }

            public void Dispose()
            {
                if (_tmp != null) _tmp.onValueChanged.RemoveListener(OnChanged);
                if (_legacy != null) _legacy.onValueChanged.RemoveListener(OnChanged);
                _tmp = null;
                _legacy = null;
                UserChanged = null;
            }
        }

        /// <summary>
        /// A read-only fill - progress bar, stat bar, radial gauge.
        /// </summary>
        /// <remarks>
        /// A filled <see cref="Image"/> rather than a Slider. These show a number and never collect
        /// one, and a Slider brought along drag handling, focus and a handle rect that all had to be
        /// disabled again. This is also what the prefab writer produces, so a screen looks the same
        /// whether it was saved as a prefab or built from the compiled program.
        ///
        /// <see cref="Value"/> is in the author's units, not normalised. Every other handle reports
        /// the real value, and a binding should not have to know that this particular control wants
        /// 0-1 while the slider beside it wants 0-100.
        /// </remarks>
        private sealed class FillHandle : INexValueHandle
        {
            private Image _image;
            private float _minimum;
            private float _maximum;
            private float _value;

            // Never raised: a fill has no user interaction to report. Declared because the
            // interface is shared with the controls that do.
            public event Action<float> UserChanged;

            public static FillHandle Create(GameObject target, in NexNodeProgram node, bool radial)
            {
                var image = target.GetComponent<Image>();
                if (image == null) image = target.AddComponent<Image>();

                image.type = Image.Type.Filled;

                if (radial)
                {
                    image.fillMethod = Image.FillMethod.Radial360;
                    image.fillOrigin = (int)Image.Origin360.Bottom;
                    image.fillClockwise = !node.TryGetProperty("value.clockwise", out var clockwise)
                                          || clockwise.Flag;
                }
                else
                {
                    ApplyDirection(image, node);
                }

                var maximum = node.ValueMax > node.ValueMin ? node.ValueMax : node.ValueMin + 1f;
                return new FillHandle { _image = image, _minimum = node.ValueMin, _maximum = maximum };
            }

            /// <summary>
            /// Sets which edge a linear fill grows from.
            /// </summary>
            /// <remarks>
            /// Matched by name, like every other enum crossing the compile path, so reordering the
            /// authoring enum cannot silently repoint existing screens. An unrecognised name falls
            /// back to left-to-right rather than leaving the bar unfilled, because a bar growing the
            /// wrong way is visibly wrong while a blank one looks like missing data.
            /// </remarks>
            private static void ApplyDirection(Image image, in NexNodeProgram node)
            {
                var direction = node.TryGetProperty("value.direction", out var property)
                    ? property.Text
                    : null;

                switch (direction)
                {
                    case "RightToLeft":
                        image.fillMethod = Image.FillMethod.Horizontal;
                        image.fillOrigin = (int)Image.OriginHorizontal.Right;
                        break;

                    case "BottomToTop":
                        image.fillMethod = Image.FillMethod.Vertical;
                        image.fillOrigin = (int)Image.OriginVertical.Bottom;
                        break;

                    case "TopToBottom":
                        image.fillMethod = Image.FillMethod.Vertical;
                        image.fillOrigin = (int)Image.OriginVertical.Top;
                        break;

                    default:
                        image.fillMethod = Image.FillMethod.Horizontal;
                        image.fillOrigin = (int)Image.OriginHorizontal.Left;
                        break;
                }
            }

            public float Value
            {
                get => _value;
                set
                {
                    _value = value;
                    if (_image == null) return;

                    // Normalised here rather than by the caller, because fillAmount is the only
                    // place that wants 0-1 and everything upstream speaks the author's range.
                    _image.fillAmount = Mathf.Clamp01(Mathf.InverseLerp(_minimum, _maximum, value));
                }
            }

            public void Dispose()
            {
                _image = null;
                UserChanged = null;
            }
        }

        /// <summary>
        /// An input field, whose value is the text the user typed.
        /// </summary>
        /// <remarks>
        /// Subscribed to <c>onValueChanged</c> rather than <c>onEndEdit</c>. Per-keystroke is what
        /// a bound field is for - a search box filtering as you type, a form validating while it is
        /// filled - and a rule that wants the committed value can watch for submit instead. The
        /// reverse is not available: end-edit gives no way to observe typing at all.
        /// </remarks>
        private sealed class InputFieldHandle : INexTextHandle
        {
            private InputField _legacy;
            private TMP_InputField _tmp;
            private bool _suppress;

            public event Action<string> UserChanged;

            public static InputFieldHandle Create(GameObject target)
            {
                // Whichever is already there wins. A screen loaded from a prefab already has its
                // field built by UGUIControlFactory, and adding a second would leave two controls
                // fighting over one rect.
                var tmp = target.GetComponent<TMP_InputField>();
                if (tmp == null && target.GetComponent<InputField>() == null) tmp = Build(target);

                if (tmp != null)
                {
                    var tmpHandle = new InputFieldHandle { _tmp = tmp };
                    tmp.onValueChanged.AddListener(tmpHandle.OnChanged);
                    return tmpHandle;
                }

                var legacy = target.GetComponent<InputField>();
                if (legacy == null) return null;

                var handle = new InputFieldHandle { _legacy = legacy };
                legacy.onValueChanged.AddListener(handle.OnChanged);
                return handle;
            }

            /// <summary>
            /// Builds the parts uGUI requires for an input field to work.
            /// </summary>
            /// <remarks>
            /// A bare <see cref="TMP_InputField"/> accepts focus and then has nowhere to put the
            /// characters - it needs a text component to render into and a viewport to clip against.
            /// Building them here rather than expecting authored child elements is what makes
            /// "drop an input field on the screen" produce a working input field, the same bargain
            /// <see cref="SliderHandle"/> makes with its fill and handle.
            ///
            /// Only reached on the compiled path. Screens saved as prefabs get the full Unity
            /// control from <c>UGUIControlFactory</c>, which is richer than this and is left alone.
            /// </remarks>
            private static TMP_InputField Build(GameObject target)
            {
                var viewport = NewChild(target, "TextArea");
                var mask = viewport.gameObject.AddComponent<RectMask2D>();
                mask.padding = new Vector4(2f, 2f, 2f, 2f);

                var textGo = new GameObject("Text", typeof(RectTransform));
                var textRect = (RectTransform)textGo.transform;
                textRect.SetParent(viewport, worldPositionStays: false);
                Stretch(textRect);

                NexPartTag.Mark(textRect, "text");

                var text = textGo.AddComponent<TextMeshProUGUI>();
                text.fontSize = 14f;
                text.color = Color.black;
                text.alignment = TextAlignmentOptions.MidlineLeft;

                // The field itself takes the clicks; the text must not intercept them or the caret
                // never appears where the user pressed.
                text.raycastTarget = false;

                // A field with no graphic is invisible to the raycaster and cannot be focused at
                // all - added only when the node brought none of its own.
                if (target.GetComponent<Graphic>() == null)
                {
                    var background = target.AddComponent<Image>();
                    background.color = Color.white;
                }

                var field = target.AddComponent<TMP_InputField>();
                field.textViewport = viewport;
                field.textComponent = text;
                field.targetGraphic = target.GetComponent<Graphic>();
                return field;
            }

            private static RectTransform NewChild(GameObject parent, string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent.transform, worldPositionStays: false);
                Stretch(rect);
                return rect;
            }

            private static void Stretch(RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            public string Text
            {
                get => _tmp != null ? _tmp.text : _legacy != null ? _legacy.text : string.Empty;
                set
                {
                    var next = value ?? string.Empty;

                    // Skipped when unchanged, not just suppressed. Assigning the same text still
                    // moves the caret to the end, so a binding echoing back what was just typed
                    // would fight the user for the cursor on every keystroke.
                    if (string.Equals(Text, next, StringComparison.Ordinal)) return;

                    _suppress = true;
                    if (_tmp != null) _tmp.text = next;
                    else if (_legacy != null) _legacy.text = next;
                    _suppress = false;
                }
            }

            private void OnChanged(string value)
            {
                if (_suppress) return;
                UserChanged?.Invoke(value ?? string.Empty);
            }

            public void Dispose()
            {
                if (_tmp != null) _tmp.onValueChanged.RemoveListener(OnChanged);
                if (_legacy != null) _legacy.onValueChanged.RemoveListener(OnChanged);
                _tmp = null;
                _legacy = null;
                UserChanged = null;
            }
        }
    }
}
