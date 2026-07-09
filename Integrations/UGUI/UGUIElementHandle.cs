using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Wraps a uGUI <see cref="GameObject"/> as a backend-independent
    /// <see cref="IUIElementHandle"/>, exposing capabilities based on the components present
    /// (Button / Slider / Image / TMP_Text / Text, plus RectTransform + CanvasGroup).
    /// This is the single place uGUI types are touched on the element side.
    /// </summary>
    public sealed class UGUIElementHandle : IUIElementHandle
    {
        private readonly GameObject _go;
        private readonly Dictionary<Type, object> _capabilities = new Dictionary<Type, object>();

        public string Id { get; }
        public UIRenderBackend Backend => UIRenderBackend.UGUI;
        public object Native => _go;

        public UGUIElementHandle(GameObject go, string id = null)
        {
            _go = go ? go : throw new ArgumentNullException(nameof(go));
            Id = id ?? go.name;
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
            var rect = _go.transform as RectTransform;
            var canvasGroup = _go.GetComponent<CanvasGroup>();

            Add<IUIVisibilityCapability>(new GoVisibility(_go));
            Add<IUITransformCapability>(new UguiTransform(_go, rect, canvasGroup));
            if (rect != null) Add<IUISizeCapability>(new UguiSize(rect));

            var button = _go.GetComponent<Button>();
            if (button != null)
            {
                Add<IUIClickCapability>(new UguiClick(button));
                Add<IUIInteractableCapability>(new UguiInteractable(button));
            }

            var slider = _go.GetComponent<Slider>();
            if (slider != null)
            {
                Add<IUIValueCapability>(new UguiValue(slider));
                if (!Has<IUIInteractableCapability>())
                    Add<IUIInteractableCapability>(new UguiSelectableInteractable(slider));
            }

            var tmp = _go.GetComponent<TMP_Text>();
            if (tmp != null) Add<IUITextCapability>(new TmpText(tmp));
            else
            {
                var text = _go.GetComponent<Text>();
                if (text != null) Add<IUITextCapability>(new LegacyText(text));
            }

            var graphic = _go.GetComponent<Graphic>();
            if (graphic != null) Add<IUIStyleCapability>(new UguiStyle(graphic));
        }

        // ---- Capability adapters -------------------------------------------

        private sealed class GoVisibility : IUIVisibilityCapability
        {
            private readonly GameObject _go;
            public GoVisibility(GameObject go) => _go = go;
            public bool Visible { get => _go.activeSelf; set => _go.SetActive(value); }
        }

        private sealed class UguiTransform : IUITransformCapability
        {
            private readonly RectTransform _rect;
            private readonly Transform _transform;
            private CanvasGroup _group;
            private readonly GameObject _go;

            public UguiTransform(GameObject go, RectTransform rect, CanvasGroup group)
            {
                _go = go;
                _rect = rect;
                _transform = go.transform;
                _group = group;
            }

            private CanvasGroup Group => _group != null ? _group : (_group = _go.AddComponent<CanvasGroup>());

            public float Opacity { get => Group.alpha; set => Group.alpha = value; }

            public Vector2 Position
            {
                get => _rect != null ? _rect.anchoredPosition : (Vector2)_transform.localPosition;
                set { if (_rect != null) _rect.anchoredPosition = value; else _transform.localPosition = value; }
            }

            public Vector3 Scale
            {
                get => _transform.localScale;
                set => _transform.localScale = value;
            }

            public float Rotation
            {
                get => _transform.localEulerAngles.z;
                set
                {
                    var e = _transform.localEulerAngles;
                    e.z = value;
                    _transform.localEulerAngles = e;
                }
            }
        }

        private sealed class UguiSize : IUISizeCapability
        {
            private readonly RectTransform _rect;
            public UguiSize(RectTransform rect) => _rect = rect;
            public Vector2 SizeDelta { get => _rect.sizeDelta; set => _rect.sizeDelta = value; }
        }

        private sealed class UguiClick : IUIClickCapability
        {
            private readonly Button _button;
            private Action _handlers;

            public UguiClick(Button button)
            {
                _button = button;
                _button.onClick.AddListener(() => _handlers?.Invoke());
            }

            public event Action Clicked
            {
                add => _handlers += value;
                remove => _handlers -= value;
            }
        }

        private sealed class UguiInteractable : IUIInteractableCapability
        {
            private readonly Button _button;
            public UguiInteractable(Button button) => _button = button;
            public bool Interactable { get => _button.interactable; set => _button.interactable = value; }
        }

        private sealed class UguiSelectableInteractable : IUIInteractableCapability
        {
            private readonly Selectable _selectable;
            public UguiSelectableInteractable(Selectable s) => _selectable = s;
            public bool Interactable { get => _selectable.interactable; set => _selectable.interactable = value; }
        }

        private sealed class UguiValue : IUIValueCapability
        {
            private readonly Slider _slider;
            public UguiValue(Slider slider) => _slider = slider;
            public float Value { get => _slider.value; set => _slider.value = value; }
            public float Min { get => _slider.minValue; set => _slider.minValue = value; }
            public float Max { get => _slider.maxValue; set => _slider.maxValue = value; }
        }

        private sealed class TmpText : IUITextCapability
        {
            private readonly TMP_Text _text;
            public TmpText(TMP_Text text) => _text = text;
            public string Text { get => _text.text; set => _text.text = value; }
        }

        private sealed class LegacyText : IUITextCapability
        {
            private readonly Text _text;
            public LegacyText(Text text) => _text = text;
            public string Text { get => _text.text; set => _text.text = value; }
        }

        private sealed class UguiStyle : IUIStyleCapability
        {
            private readonly Graphic _graphic;
            private bool _warnedSetClass;
            public UguiStyle(Graphic graphic) => _graphic = graphic;

            public void SetClass(string className, bool on)
            {
                if (_warnedSetClass) return;
                _warnedSetClass = true;
                Debug.LogWarning(
                    "[NexUI] UGUI has no style classes; SetClass is a no-op. Use token/color styling instead.");
            }

            public void ApplyToken(string tokenKey, string value)
            {
                if (string.IsNullOrEmpty(tokenKey) || value == null) return;
                if (tokenKey.StartsWith("color.") && ColorUtility.TryParseHtmlString(value, out var c))
                    _graphic.color = c;
            }
        }
    }
}
