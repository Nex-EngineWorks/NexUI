using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// A ring that fills by value - cast bars, ability charge, segmented-free progress. uGUI can do
    /// this with a filled Image, but only with a sprite that already looks like a ring.
    /// </summary>
    /// <remarks>
    /// Drawn as an annulus so the thickness is a property rather than a property of the artwork.
    /// That is the whole reason this exists: a filled Image needs a new sprite for every combination
    /// of radius and thickness, and a HUD ends up with a folder of near-identical rings.
    /// </remarks>
    [AddComponentMenu("NexUI/Feedback/NX Radial Fill")]
    public sealed class NXRadialFill : MaskableGraphic, INXRadialFill
    {
        [SerializeField, Range(0f, 1f)] private float m_Fill = 0.75f;
        [SerializeField, Tooltip("Ring thickness in pixels. 0 fills to the centre like a pie.")]
        private float m_Thickness = 12f;
        [SerializeField, Tooltip("Where the fill starts, in degrees. 90 is the top.")]
        private float m_StartAngle = 90f;
        [SerializeField] private bool m_Clockwise = true;
        [SerializeField, Range(8, 180), Tooltip("Segments over a full turn. Higher is smoother.")]
        private int m_Segments = 72;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public float Fill
        {
            get => m_Fill;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, m_Fill)) return;
                m_Fill = clamped;
                SetVerticesDirty();
            }
        }

        /// <inheritdoc/>
        public bool Clockwise
        {
            get => m_Clockwise;
            set { m_Clockwise = value; SetVerticesDirty(); }
        }

        public float Thickness
        {
            get => m_Thickness;
            set { m_Thickness = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (m_Fill <= 0f) return;

            var rect = GetPixelAdjustedRect();
            var centre = rect.center;
            var outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (outer <= 0f) return;

            // 0 thickness means "fill to the centre", which is a pie rather than a ring. Clamping
            // instead of special-casing keeps one code path for both.
            var inner = m_Thickness <= 0f ? 0f : Mathf.Clamp(outer - m_Thickness, 0f, outer);

            var steps = Mathf.Max(1, Mathf.CeilToInt(m_Segments * m_Fill));
            var sweep = 360f * m_Fill * (m_Clockwise ? -1f : 1f);

            for (var i = 0; i <= steps; i++)
            {
                var radians = (m_StartAngle + sweep * (i / (float)steps)) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                vertexHelper.AddVert(centre + direction * inner, color, Vector2.zero);
                vertexHelper.AddVert(centre + direction * outer, color, Vector2.one);

                if (i == 0) continue;
                var v = i * 2;
                vertexHelper.AddTriangle(v - 2, v - 1, v + 1);
                vertexHelper.AddTriangle(v - 2, v + 1, v);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_Fill = Mathf.Clamp01(m_Fill);
            SetVerticesDirty();
        }
#endif
    }

    /// <summary>
    /// Indeterminate loading indicator: spins its own transform for as long as it is showing
    /// something the game cannot put a percentage on.
    /// </summary>
    /// <remarks>
    /// Unscaled time on purpose. A spinner that stops because the game paused - or because a loading
    /// screen set <c>timeScale</c> to zero - reads as a freeze, which is the exact impression the
    /// spinner exists to prevent.
    /// </remarks>
    [AddComponentMenu("NexUI/Feedback/NX Spinner")]
    public sealed class NXSpinner : UIBehaviour, INXSpinner
    {
        [SerializeField] private bool m_Spinning = true;
        [SerializeField, Tooltip("Degrees per second. Negative spins the other way.")]
        private float m_Speed = 240f;

        private RectTransform _rect;
        private float _angle;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public bool Spinning
        {
            get => m_Spinning;
            set => m_Spinning = value;
        }

        /// <inheritdoc/>
        public float Speed
        {
            get => m_Speed;
            set => m_Speed = value;
        }

        protected override void Awake()
        {
            base.Awake();
            _rect = transform as RectTransform;
        }

        private void Update()
        {
            if (!m_Spinning || _rect == null) return;
            _angle = Mathf.Repeat(_angle - m_Speed * UnityTime.unscaledDeltaTime, 360f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, _angle);
        }
    }

    /// <summary>
    /// Placeholder shown while real content loads - the grey blocks that keep a list from jumping
    /// around once data arrives.
    /// </summary>
    /// <remarks>
    /// Toggling <see cref="Active"/> swaps the placeholder for the real content rather than only
    /// hiding itself, because the two always move together and leaving that to each caller is how
    /// screens end up showing both at once for a frame.
    /// </remarks>
    [AddComponentMenu("NexUI/Feedback/NX Skeleton")]
    public sealed class NXSkeleton : UIBehaviour, INXSkeleton
    {
        [SerializeField, Tooltip("Shown while loading.")] private GameObject m_Placeholder;
        [SerializeField, Tooltip("Shown once loading finished.")] private GameObject m_Content;
        [SerializeField] private bool m_Active = true;
        [SerializeField, Tooltip("Shimmer sweeps per second. 0 disables the shimmer entirely.")]
        private float m_ShimmerSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float m_ShimmerDepth = 0.35f;
        [SerializeField] private Graphic m_ShimmerTarget;

        private float _phase;
        private float _baseAlpha = 1f;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public bool Active
        {
            get => m_Active;
            set
            {
                if (m_Active == value) return;
                m_Active = value;
                Apply();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (m_ShimmerTarget != null) _baseAlpha = m_ShimmerTarget.color.a;
            Apply();
        }

        private void Apply()
        {
            if (m_Placeholder != null) m_Placeholder.SetActive(m_Active);
            if (m_Content != null) m_Content.SetActive(!m_Active);
        }

        private void Update()
        {
            if (!m_Active || m_ShimmerTarget == null || m_ShimmerSpeed <= 0f) return;

            _phase = Mathf.Repeat(_phase + m_ShimmerSpeed * UnityTime.unscaledDeltaTime, 1f);
            var wave = (Mathf.Sin(_phase * Mathf.PI * 2f) + 1f) * 0.5f;
            var color = m_ShimmerTarget.color;
            color.a = _baseAlpha * Mathf.Lerp(1f - m_ShimmerDepth, 1f, wave);
            m_ShimmerTarget.color = color;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_ShimmerTarget == null) return;
            var color = m_ShimmerTarget.color;
            color.a = _baseAlpha;
            m_ShimmerTarget.color = color;
        }
    }

    /// <summary>
    /// Transient message with a severity and an auto-dismiss - "Saved", "Connection lost",
    /// "Item added". Unity has nothing for this, so every project rebuilds it.
    /// </summary>
    /// <remarks>
    /// The countdown runs on unscaled time and pauses while the pointer is over the toast, so a
    /// message does not vanish out from under someone who is reading it. Dismissal raises
    /// <see cref="Dismissed"/> rather than destroying the object: pooling toasts is the normal case
    /// and a component that destroys itself cannot be pooled.
    /// </remarks>
    [AddComponentMenu("NexUI/Feedback/NX Toast")]
    public sealed class NXToast : UIBehaviour, INXToast, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, TextArea] private string m_Message = "";
        [SerializeField] private NXToastSeverity m_Severity = NXToastSeverity.Info;
        [SerializeField, Tooltip("Seconds before it dismisses itself. 0 waits for an explicit Dismiss().")]
        private float m_Duration = 3f;
        [SerializeField, Tooltip("Label the message is written into.")] private Graphic m_Label;
        [SerializeField, Tooltip("Graphic tinted by severity.")] private Graphic m_Accent;

        [SerializeField] private Color m_InfoColor = new Color(0.25f, 0.55f, 0.95f);
        [SerializeField] private Color m_SuccessColor = new Color(0.25f, 0.72f, 0.42f);
        [SerializeField] private Color m_WarningColor = new Color(0.92f, 0.68f, 0.22f);
        [SerializeField] private Color m_ErrorColor = new Color(0.86f, 0.31f, 0.31f);

        [SerializeField] private UnityEvent m_OnDismissed = new UnityEvent();

        private float _remaining;
        private bool _counting;
        private bool _hovered;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <summary>Raised when the toast finished, however it finished.</summary>
        public UnityEvent Dismissed => m_OnDismissed;

        /// <inheritdoc/>
        public string Message
        {
            get => m_Message;
            set { m_Message = value; ApplyMessage(); }
        }

        /// <inheritdoc/>
        public NXToastSeverity Severity
        {
            get => m_Severity;
            set { m_Severity = value; ApplySeverity(); }
        }

        /// <inheritdoc/>
        public float Duration
        {
            get => m_Duration;
            set => m_Duration = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyMessage();
            ApplySeverity();
            Restart();
        }

        /// <summary>Starts the countdown again - what a pooled toast needs on reuse.</summary>
        public void Restart()
        {
            _remaining = m_Duration;
            _counting = m_Duration > 0f;
        }

        /// <summary>Ends the toast now and raises <see cref="Dismissed"/>.</summary>
        public void Dismiss()
        {
            if (!isActiveAndEnabled && !_counting) return;
            _counting = false;
            m_OnDismissed.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        private void Update()
        {
            if (!_counting || _hovered) return;
            _remaining -= UnityTime.unscaledDeltaTime;
            if (_remaining > 0f) return;
            Dismiss();
        }

        private void ApplyMessage()
        {
            if (m_Label == null) return;
            var text = m_Label.GetComponent<TMPro.TMP_Text>();
            if (text != null) { text.text = m_Message; return; }
            var legacy = m_Label as Text;
            if (legacy != null) legacy.text = m_Message;
        }

        private void ApplySeverity()
        {
            if (m_Accent == null) return;
            m_Accent.color = m_Severity switch
            {
                NXToastSeverity.Success => m_SuccessColor,
                NXToastSeverity.Warning => m_WarningColor,
                NXToastSeverity.Error => m_ErrorColor,
                _ => m_InfoColor
            };
        }
    }

    /// <summary>
    /// A list of choices with single or multiple selection - difficulty pickers, graphics presets,
    /// quest filters. uGUI has ToggleGroup, which enforces exactly one and reports nothing useful.
    /// </summary>
    /// <remarks>
    /// Option views are pooled from a template rather than rebuilt, so changing the options of a
    /// settings row does not allocate a new hierarchy every time. Selection is stored as indices
    /// because that is what a binding writes back - option text is display, not identity.
    /// </remarks>
    [AddComponentMenu("NexUI/Data/NX Choice List")]
    public sealed class NXChoiceList : UIBehaviour, INXChoiceList
    {
        [SerializeField] private bool m_AllowMultiple;
        [SerializeField, Tooltip("Toggle used as the template for each option. Kept inactive.")]
        private Toggle m_OptionTemplate;
        [SerializeField, Tooltip("Parent the option views are created under. Defaults to this element.")]
        private RectTransform m_Container;
        [SerializeField] private List<string> m_Options = new List<string>();

        [SerializeField] private UnityEvent<int> m_OnSelectionChanged = new UnityEvent<int>();

        private readonly List<Toggle> _views = new List<Toggle>();
        private readonly List<int> _selected = new List<int>();
        private bool _suppress;

        /// <inheritdoc/>
        public IUIElementHandle Handle { get; set; }

        /// <inheritdoc/>
        public event System.Action<IReadOnlyList<int>> SelectionChanged;

        /// <summary>Inspector-friendly mirror of <see cref="SelectionChanged"/>, carrying the first index.</summary>
        public UnityEvent<int> OnSelectionChanged => m_OnSelectionChanged;

        /// <inheritdoc/>
        public bool AllowMultiple
        {
            get => m_AllowMultiple;
            set
            {
                m_AllowMultiple = value;
                if (value || _selected.Count <= 1) return;

                // Collapsing to single selection keeps the first choice: it is the one the user
                // made earliest, and silently keeping the last would undo a deliberate pick.
                var keep = _selected[0];
                _selected.Clear();
                _selected.Add(keep);
                SyncViews();
                Raise();
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> Options => m_Options;

        /// <inheritdoc/>
        public IReadOnlyList<int> SelectedIndices => _selected;

        protected override void Awake()
        {
            base.Awake();
            if (m_Container == null) m_Container = transform as RectTransform;
            if (m_OptionTemplate != null) m_OptionTemplate.gameObject.SetActive(false);
            Rebuild();
        }

        /// <inheritdoc/>
        public void SetOptions(IReadOnlyList<string> options)
        {
            m_Options.Clear();
            if (options != null)
                for (var i = 0; i < options.Count; i++) m_Options.Add(options[i]);

            _selected.Clear();
            Rebuild();
            Raise();
        }

        /// <inheritdoc/>
        public void Select(int index, bool selected)
        {
            if (index < 0 || index >= m_Options.Count) return;

            var changed = false;
            if (selected)
            {
                if (!m_AllowMultiple && (_selected.Count != 1 || _selected[0] != index))
                {
                    _selected.Clear();
                    _selected.Add(index);
                    changed = true;
                }
                else if (m_AllowMultiple && !_selected.Contains(index))
                {
                    _selected.Add(index);
                    changed = true;
                }
            }
            else
            {
                changed = _selected.Remove(index);
            }

            if (!changed) return;
            SyncViews();
            Raise();
        }

        private void Rebuild()
        {
            for (var i = 0; i < _views.Count; i++)
                if (_views[i] != null) Destroy(_views[i].gameObject);
            _views.Clear();

            if (m_OptionTemplate == null || m_Container == null) return;

            for (var i = 0; i < m_Options.Count; i++)
            {
                var view = Instantiate(m_OptionTemplate, m_Container);
                view.gameObject.SetActive(true);
                view.gameObject.name = "Option " + i;

                var label = view.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null) label.text = m_Options[i];

                var index = i;
                view.onValueChanged.AddListener(on =>
                {
                    if (_suppress) return;
                    Select(index, on);
                });
                _views.Add(view);
            }

            SyncViews();
        }

        private void SyncViews()
        {
            // Writing isOn fires onValueChanged, which would call back into Select and fight the
            // change already in progress. The flag is what keeps one user click to one event.
            _suppress = true;
            for (var i = 0; i < _views.Count; i++)
                if (_views[i] != null) _views[i].isOn = _selected.Contains(i);
            _suppress = false;
        }

        private void Raise()
        {
            SelectionChanged?.Invoke(_selected);
            m_OnSelectionChanged.Invoke(_selected.Count > 0 ? _selected[0] : -1);
        }
    }
}
