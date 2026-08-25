using System.Text;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// A screen after compilation: the only NexUI artifact a player build reads.
    /// </summary>
    /// <remarks>
    /// The runtime never opens an authoring metadata asset. Everything the player needs is
    /// resolved, validated and flattened into this asset at edit time, which buys three things:
    /// the authoring model can change without breaking shipped content, authoring-only data
    /// (preview values, editor visibility, designer notes) never reaches the build, and a screen
    /// that failed validation simply has no compiled asset rather than failing halfway through
    /// instantiation on a player device.
    ///
    /// A <c>ScriptableObject</c> rather than a JSON blob because Unity then handles the asset
    /// reference graph, addressables/resources loading and IL2CPP serialization for free, and
    /// sprite / font references stay real references instead of paths resolved at runtime.
    /// </remarks>
    [PreferBinarySerialization]
    public sealed class NexScreenProgram : ScriptableObject
    {
        /// <summary>
        /// Bumped whenever the meaning of the compiled format changes. The runtime refuses a
        /// program from a different version (NEX-RT-6003) rather than guessing, because a
        /// silently misread program produces a screen that is subtly wrong instead of absent.
        /// </summary>
        /// <remarks>
        /// 6: nodes carry <see cref="NexLayoutProgram"/>. A version 5 runtime would ignore it and
        /// lay the screen out as fixed rects, which is wrong rather than merely incomplete - hence
        /// a bump rather than a tolerated addition.
        ///
        /// 7: the program carries <see cref="NexStateProgram"/>, <see cref="NexResponsiveProgram"/>
        /// and <see cref="NexPartProgram"/>, nodes carry parent-resize constraints, and nodes carry
        /// a localization key. A version 6 runtime would show every screen in its base state with no
        /// way to reach the others, lay every screen out as if it were the reference resolution, pin
        /// every node to its parent's top-left, and show untranslated text - all wrong rather than
        /// merely incomplete. One bump covers them because no version 7 asset was ever published
        /// between them.
        /// </remarks>
        public const int CurrentCompilerVersion = 7;

        [SerializeField] private int _compilerVersion = CurrentCompilerVersion;
        [SerializeField] private string _screenId;
        [SerializeField] private string _contentHash;
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private NexNodeProgram[] _nodes = new NexNodeProgram[0];
        [SerializeField] private NexSourceMap _sourceMap = new NexSourceMap();
        [SerializeField] private NexFeatureManifest _features = new NexFeatureManifest();
        [SerializeField] private NexInteractionProgram _interactions = new NexInteractionProgram();
        [SerializeField] private NexStateProgram _states = new NexStateProgram();
        [SerializeField] private NexResponsiveProgram _responsive = new NexResponsiveProgram();
        [SerializeField] private NexPartProgram _parts = new NexPartProgram();

        public int CompilerVersion => _compilerVersion;

        /// <summary>The key <c>UIManager</c> opens this screen by.</summary>
        public string ScreenId => _screenId;

        /// <summary>
        /// Hash of the authoring input plus compiler settings. Equal hashes mean an identical
        /// program, which is what makes the compile cache and the determinism test possible.
        /// </summary>
        public string ContentHash => _contentHash;

        public Vector2 ReferenceResolution => _referenceResolution;

        /// <summary>Nodes in instantiation order: a parent always precedes its children.</summary>
        public NexNodeProgram[] Nodes => _nodes;

        public NexSourceMap SourceMap => _sourceMap;

        public NexFeatureManifest Features => _features;

        /// <summary>Compiled trigger / condition / action rules. Never null; may be empty.</summary>
        public NexInteractionProgram Interactions => _interactions;

        /// <summary>Compiled state deltas. Never null; may be empty.</summary>
        public NexStateProgram States => _states;

        /// <summary>Compiled responsive rules. Never null; may be empty.</summary>
        public NexResponsiveProgram Responsive => _responsive;

        /// <summary>Compiled internal-part nudges. Never null; may be empty.</summary>
        public NexPartProgram Parts => _parts;

        /// <summary>
        /// Fills in a freshly created instance. Only the compiler calls this - the asset is
        /// immutable once published, and the runtime has no reason to mutate it.
        /// </summary>
        public void Initialize(string screenId, NexNodeProgram[] nodes, NexSourceMap sourceMap,
            NexFeatureManifest features, Vector2 referenceResolution, string contentHash,
            NexInteractionProgram interactions = null, NexStateProgram states = null,
            NexResponsiveProgram responsive = null, NexPartProgram parts = null)
        {
            _compilerVersion = CurrentCompilerVersion;
            _screenId = screenId ?? string.Empty;
            _nodes = nodes ?? new NexNodeProgram[0];
            _sourceMap = sourceMap ?? new NexSourceMap();
            _features = features ?? new NexFeatureManifest();
            _interactions = interactions ?? new NexInteractionProgram();
            _states = states ?? new NexStateProgram();
            _responsive = responsive ?? new NexResponsiveProgram();
            _parts = parts ?? new NexPartProgram();
            _referenceResolution = referenceResolution;
            _contentHash = contentHash ?? string.Empty;
        }

        /// <summary>Node index for an authoring stable id, or -1.</summary>
        public int IndexOfNode(string nodeId) => _sourceMap.IndexOf(nodeId);

        /// <summary>
        /// Node index for an automation id, or -1.
        /// </summary>
        /// <remarks>
        /// A linear scan rather than a prebuilt map. Automation ids are used by tests, once per
        /// lookup, on screens with tens of nodes - a dictionary would cost memory in every shipped
        /// build to speed up something no frame ever does. The compiler has already guaranteed the
        /// ids are unique, so the first match is the only match.
        /// </remarks>
        public int IndexOfAutomationId(string automationId)
        {
            if (string.IsNullOrEmpty(automationId)) return -1;

            for (int i = 0; i < _nodes.Length; i++)
                if (string.Equals(_nodes[i].AutomationId, automationId, System.StringComparison.Ordinal))
                    return i;

            return -1;
        }

        /// <summary>
        /// Canonical text form of the program. Two programs with the same canonical form are the
        /// same program; this is what the content hash is computed over and what the determinism
        /// test diffs, so it must include everything that affects runtime behaviour and nothing
        /// that does not (asset guids, timestamps, machine paths).
        /// </summary>
        public string ToCanonicalString()
        {
            var sb = new StringBuilder();
            sb.Append("screen:").Append(_screenId).Append('\n');
            sb.Append("compiler:").Append(_compilerVersion).Append('\n');
            sb.Append("reference:").Append(Fixed(_referenceResolution.x)).Append(',')
              .Append(Fixed(_referenceResolution.y)).Append('\n');

            for (int i = 0; i < _nodes.Length; i++)
            {
                var n = _nodes[i];
                sb.Append("node:").Append(i)
                  .Append('|').Append(n.NodeId)
                  .Append('|').Append(n.Name)
                  .Append('|').Append(n.ParentIndex)
                  .Append('|').Append(n.Kind)
                  .Append('|').Append(Fixed(n.Rect.x)).Append(',').Append(Fixed(n.Rect.y))
                  .Append(',').Append(Fixed(n.Rect.width)).Append(',').Append(Fixed(n.Rect.height))
                  .Append('|').Append(n.Anchor)
                  .Append('|').Append(ColorText(n.Tint))
                  .Append('|').Append(ColorText(n.TextColor))
                  .Append('|').Append(n.FontSize)
                  .Append('|').Append(n.Text)
                  .Append('|').Append(n.Visible ? 1 : 0)
                  .Append('|').Append(n.TextBindingKey)
                  .Append('|').Append(n.CommandId)
                  .Append('|').Append(n.AutomationId)
                  .Append('|').Append(n.Role)

                  // Everything below has to be here for the same reason the fields above are: the
                  // publisher skips writing when the hash is unchanged, so a field the hash omits
                  // is a field the author can edit without the change ever reaching the asset.
                  .Append('|').Append(n.ValueBindingKey)
                  .Append('|').Append(n.VisibilityBindingKey)
                  .Append('|').Append(n.InteractableBindingKey)
                  .Append('|').Append(n.ClassBindingKey)
                  .Append('|').Append(n.TextBindingMode)
                  .Append('|').Append(n.ValueBindingMode)
                  .Append('|').Append(n.TextConverterKey)
                  .Append('|').Append(n.ValueConverterKey)
                  .Append('|').Append(n.AccessibilityLabel)
                  .Append('|').Append(n.LocalizationKey)
                  .Append('|').Append(n.FocusOrder)
                  .Append('|').Append((int)n.Capabilities)
                  .Append('|').Append(n.ControlId)
                  .Append('|').Append(Fixed(n.ValueMin)).Append(',').Append(Fixed(n.ValueMax));

                AppendProperties(sb, n.ControlProperties);
                AppendLayout(sb, n.Layout);
                AppendAppearance(sb, n.Appearance);
                AppendTypography(sb, n.Typography);
                AppendStyle(sb, n.Style);
                AppendMotion(sb, n.Motion);
                AppendShape(sb, n.Shape);
                sb.Append('\n');
            }

            for (int i = 0; i < _features.Requirements.Count; i++)
            {
                var r = _features.Requirements[i];
                sb.Append("feature:").Append(r.FeatureId).Append('|').Append(r.NodeId).Append('\n');
            }

            for (int i = 0; i < _interactions.Rules.Count; i++)
            {
                var rule = _interactions.Rules[i];
                sb.Append("rule:").Append(rule.RuleId)
                  .Append('|').Append(rule.NodeIndex)
                  .Append('|').Append(rule.Trigger)
                  .Append('|').Append(rule.Phase)
                  .Append('|').Append(rule.StopsPropagation ? 1 : 0)
                  .Append('|').Append(rule.HasCondition ? 1 : 0)
                  .Append('|').Append(rule.ConditionKey)
                  .Append('|').Append(rule.Comparison)
                  .Append('|').Append(rule.ConditionString)
                  .Append('|').Append(rule.ConditionIsNumeric ? Fixed((float)rule.ConditionNumber) : string.Empty)
                  .Append('|').Append(rule.ActionStart).Append(',').Append(rule.ActionCount)
                  .Append('\n');
            }

            for (int i = 0; i < _interactions.Actions.Count; i++)
            {
                var a = _interactions.Actions[i];
                sb.Append("action:").Append(i)
                  .Append('|').Append(a.Kind)
                  .Append('|').Append(a.CommandId)
                  .Append('|').Append(a.StateKey)
                  .Append('|').Append(a.StringValue)
                  .Append('|').Append(a.IsNumeric ? Fixed((float)a.NumberValue) : string.Empty)
                  .Append('|').Append(a.BoolValue ? 1 : 0)
                  .Append('|').Append(a.TargetNodeIndex)
                  .Append('|').Append(Fixed((float)a.Seconds))
                  .Append('\n');
            }

            AppendStates(sb, _states);
            AppendResponsive(sb, _responsive);
            AppendParts(sb, _parts);

            return sb.ToString();
        }

        /// <summary>
        /// Appends the screen's states to the canonical form.
        /// </summary>
        /// <remarks>
        /// Written after the nodes rather than beside them because a state is a screen-level fact:
        /// one state usually touches several nodes, and hanging its deltas off each node would
        /// scatter one authored edit across the form.
        ///
        /// Delta order is preserved rather than sorted, for the reason class order is: two deltas
        /// may write the same property of the same node, and then the last one wins - so reordering
        /// them is a behaviour change the hash has to see.
        ///
        /// Nothing is written when no state is authored, so adding state support did not change the
        /// hash of any existing screen and did not invalidate every published asset at once.
        /// </remarks>
        private static void AppendStates(StringBuilder sb, NexStateProgram states)
        {
            if (states == null || states.IsEmpty) return;

            for (int i = 0; i < states.States.Count; i++)
            {
                var entry = states.States[i];
                sb.Append("state:").Append(entry.StateId)
                  .Append('|').Append(entry.DisplayName)
                  .Append('|').Append(entry.IsDefault ? 1 : 0);

                for (int d = 0; d < entry.DeltaCount; d++)
                {
                    var delta = states.Deltas[entry.DeltaStart + d];
                    sb.Append("|d:").Append(delta.NodeIndex).Append(':');
                    AppendProperty(sb, delta.Value);
                }

                sb.Append('\n');
            }
        }

        /// <summary>
        /// Floats are written at fixed precision and with an invariant culture. Without both, the
        /// same screen hashes differently on a machine with a comma decimal separator, or after a
        /// round-trip that costs a bit of mantissa - and the cache starts missing for no reason.
        /// </summary>
        /// <summary>
        /// Appends authored control properties in the order the compiler emitted them.
        /// </summary>
        /// <remarks>
        /// Not sorted here. The compiler walks the schema, which is a fixed list, so the order is
        /// already deterministic for a given Studio version - and sorting would hide a compiler
        /// that started emitting them in a data-dependent order, which is exactly the kind of
        /// non-determinism the hash exists to catch.
        /// </remarks>
        private static void AppendProperties(StringBuilder sb, NexNodeProperty[] properties)
        {
            if (properties == null || properties.Length == 0) return;

            for (int i = 0; i < properties.Length; i++)
            {
                sb.Append("|p:");
                AppendProperty(sb, properties[i]);
            }
        }

        /// <summary>
        /// Writes one keyed property as <c>key=value</c>.
        /// </summary>
        /// <remarks>
        /// Shared with the state table so a delta and a control property that carry the same value
        /// hash identically. Two formatters for one value type is how a round trip starts producing
        /// a different hash than the compile that wrote it.
        /// </remarks>
        private static void AppendProperty(StringBuilder sb, NexNodeProperty property)
        {
            sb.Append(property.Key).Append('=');

            switch (property.Kind)
            {
                case NexPropertyKind.Flag: sb.Append(property.Flag ? 1 : 0); break;
                case NexPropertyKind.Text: sb.Append(property.Text); break;
                case NexPropertyKind.Color: sb.Append(ColorText(property.Color)); break;
                case NexPropertyKind.Vector:
                    sb.Append(Fixed(property.Vector.x)).Append(',').Append(Fixed(property.Vector.y));
                    break;
                default: sb.Append(Fixed(property.Number)); break;
            }
        }

        /// <summary>
        /// Appends the screen's internal-part nudges to the canonical form.
        /// </summary>
        /// <remarks>
        /// The has-flags are written as well as the values. "Unset" and "set to zero" are different
        /// authoring statements - one leaves the control's own baseline alone, the other pins the
        /// part to it - and a form that wrote only the value would hash them the same.
        /// </remarks>
        private static void AppendParts(StringBuilder sb, NexPartProgram parts)
        {
            if (parts == null || parts.IsEmpty) return;

            for (int i = 0; i < parts.Overrides.Count; i++)
            {
                var part = parts.Overrides[i];
                sb.Append("part:").Append(part.NodeIndex).Append('|').Append(part.PartId)
                  .Append('|').Append(part.HasPosition ? 1 : 0)
                  .Append(',').Append(Fixed(part.Position.x)).Append(';').Append(Fixed(part.Position.y))
                  .Append('|').Append(part.HasSizeDelta ? 1 : 0)
                  .Append(',').Append(Fixed(part.SizeDelta.x)).Append(';').Append(Fixed(part.SizeDelta.y))
                  .Append('|').Append(part.HasRotation ? 1 : 0).Append(',').Append(Fixed(part.Rotation))
                  .Append('|').Append(part.HasScale ? 1 : 0)
                  .Append(',').Append(Fixed(part.Scale.x)).Append(';').Append(Fixed(part.Scale.y))
                  .Append('|').Append(part.HasVisibility ? 1 : 0).Append(',').Append(part.Visible ? 1 : 0)
                  .Append('\n');
            }
        }

        /// <summary>
        /// Appends the screen's responsive rules to the canonical form.
        /// </summary>
        /// <remarks>
        /// The condition is written as well as the deltas. A rule whose breakpoint moved changes
        /// which screens it applies to, which is a behaviour change even when every delta under it
        /// is untouched - and a hash that missed it would leave the published asset saying the old
        /// breakpoint.
        /// </remarks>
        private static void AppendResponsive(StringBuilder sb, NexResponsiveProgram responsive)
        {
            if (responsive == null || responsive.IsEmpty) return;

            for (int i = 0; i < responsive.Rules.Count; i++)
            {
                var rule = responsive.Rules[i];
                sb.Append("responsive:").Append(rule.RuleId)
                  .Append('|').Append(rule.MinResolution.x).Append(',').Append(rule.MinResolution.y)
                  .Append('|').Append(rule.MaxResolution.x).Append(',').Append(rule.MaxResolution.y)
                  .Append('|').Append(rule.ConstrainInputMode ? (int)rule.InputMode : -1);

                for (int d = 0; d < rule.DeltaCount; d++)
                {
                    var delta = responsive.Deltas[rule.DeltaStart + d];
                    sb.Append("|d:").Append(delta.NodeIndex).Append(':');
                    AppendProperty(sb, delta.Value);
                }

                sb.Append('\n');
            }
        }

        /// <summary>
        /// Appends a node's layout to the canonical form.
        /// </summary>
        /// <remarks>
        /// Omitted entirely when the layout is default, which is most nodes - so adding layout
        /// support did not change the hash of any screen that does not use it, and existing
        /// published assets were not all invalidated at once.
        ///
        /// Every field is written for the reason the binding fields above are: the publisher skips
        /// writing when the hash is unchanged, so a field omitted here is a field the author can
        /// edit with no effect on the build.
        /// </remarks>
        private static void AppendLayout(StringBuilder sb, NexLayoutProgram layout)
        {
            if (layout.IsDefault) return;

            sb.Append("|layout:").Append((int)layout.Mode)
              .Append(',').Append(Fixed(layout.Spacing))
              .Append(',').Append(Vector4Text(layout.Padding))
              .Append(',').Append(layout.GridColumns)
              .Append(',').Append(Fixed(layout.GridCellSize.x)).Append(';').Append(Fixed(layout.GridCellSize.y))
              .Append(',').Append((int)layout.Wrap)
              .Append(',').Append((int)layout.Align)
              .Append(',').Append((int)layout.Justify)
              .Append(',').Append((int)layout.WidthSizing)
              .Append(',').Append((int)layout.HeightSizing)
              .Append(',').Append(Fixed(layout.MinSize.x)).Append(';').Append(Fixed(layout.MinSize.y))
              .Append(',').Append(Fixed(layout.MaxSize.x)).Append(';').Append(Fixed(layout.MaxSize.y))
              .Append(',').Append(Vector4Text(layout.Margin))
              .Append(',').Append(Fixed(layout.AspectRatio))
              .Append(',').Append((int)layout.HorizontalConstraint)
              .Append(',').Append((int)layout.VerticalConstraint);
        }

        /// <summary>
        /// Appends a node's appearance to the canonical form.
        /// </summary>
        /// <remarks>
        /// Omitted when neutral, for the same reason layout is: a screen that uses no effects must
        /// hash as it did before appearance was carried, or adding the feature would have
        /// invalidated every published asset at once.
        /// </remarks>
        private static void AppendAppearance(StringBuilder sb, NexAppearanceProgram appearance)
        {
            if (appearance.IsNeutral) return;

            sb.Append("|look:").Append(Fixed(appearance.Opacity))
              .Append(',').Append(Fixed(appearance.BorderWidth))
              .Append(',').Append(ColorText(appearance.BorderColor))
              .Append(',').Append(Fixed(appearance.CornerRadius))
              .Append(',').Append(appearance.DropShadow ? 1 : 0)
              .Append(',').Append(ColorText(appearance.ShadowColor))
              .Append(',').Append(Fixed(appearance.ShadowOffset.x)).Append(';').Append(Fixed(appearance.ShadowOffset.y))
              .Append(',').Append(Fixed(appearance.ShadowBlur))
              .Append(',').Append(appearance.InnerShadow ? 1 : 0)
              .Append(',').Append(Fixed(appearance.OutlineWidth))
              .Append(',').Append(ColorText(appearance.OutlineColor))
              .Append(',').Append(Fixed(appearance.Blur))
              .Append(',').Append(appearance.Mask ? 1 : 0)
              .Append(',').Append(appearance.ImageSlice ? 1 : 0)
              .Append(',').Append((int)appearance.ImageFit)
              .Append(',').Append(appearance.Crop ? 1 : 0);
        }

        /// <summary>
        /// Appends a node's typography to the canonical form.
        /// </summary>
        /// <remarks>
        /// Skipped entirely unless the author opened the typography section, so a screen that never
        /// touched type hashes as it did before typography was carried.
        /// </remarks>
        private static void AppendTypography(StringBuilder sb, NexTypographyProgram type)
        {
            if (!type.HasOverrides) return;

            sb.Append("|type:").Append((int)type.Weight)
              .Append(',').Append((int)type.Style)
              .Append(',').Append(Fixed(type.FontSize))
              .Append(',').Append(type.AutoSize ? 1 : 0)
              .Append(',').Append(Fixed(type.MinFontSize)).Append(';').Append(Fixed(type.MaxFontSize))
              .Append(',').Append((int)type.Alignment)
              .Append(',').Append(type.Wrapping ? 1 : 0)
              .Append(',').Append((int)type.Overflow)
              .Append(',').Append(type.Ellipsis ? 1 : 0)
              .Append(',').Append(Fixed(type.LineHeight))
              .Append(',').Append(Fixed(type.LetterSpacing))
              .Append(',').Append(Fixed(type.ParagraphSpacing))
              .Append(',').Append(type.RichText ? 1 : 0)
              .Append(',').Append(type.RightToLeft ? 1 : 0)
              .Append(',').Append(ColorText(type.Color))
              .Append(',').Append(type.TextShadow ? 1 : 0)
              .Append(',').Append(ColorText(type.ShadowColor))
              .Append(',').Append(Fixed(type.ShadowOffset.x)).Append(';').Append(Fixed(type.ShadowOffset.y))
              .Append(',').Append(Fixed(type.OutlineWidth))
              .Append(',').Append(ColorText(type.OutlineColor));
        }

        /// <summary>
        /// Appends a node's classes, theme and token overrides to the canonical form.
        /// </summary>
        /// <remarks>
        /// Class order is preserved rather than sorted: a later class overriding an earlier one is
        /// how cascading works, so reordering them is a behaviour change the hash must see.
        /// </remarks>
        private static void AppendStyle(StringBuilder sb, NexStyleProgram style)
        {
            if (style.IsEmpty) return;

            sb.Append("|style:").Append(style.ThemeId);

            if (style.Classes != null)
                for (int i = 0; i < style.Classes.Length; i++)
                    sb.Append(",c=").Append(style.Classes[i]);

            if (style.TokenOverrides != null)
                for (int i = 0; i < style.TokenOverrides.Length; i++)
                    sb.Append(",t=").Append(style.TokenOverrides[i].Key)
                      .Append('=').Append(style.TokenOverrides[i].Value);
        }

        /// <summary>
        /// Appends a node's declared motion to the canonical form.
        /// </summary>
        /// <remarks>
        /// Written even though no compiled backend plays it yet. The hash is what makes the
        /// publisher rewrite the asset, so omitting it would mean that wiring a motion player in
        /// later found every published screen missing the motion its author had already set.
        /// </remarks>
        private static void AppendMotion(StringBuilder sb, NexMotionProgram motion)
        {
            if (motion.IsEmpty) return;

            sb.Append("|motion:").Append(motion.MotionId)
              .Append(',').Append(motion.InitialVariant)
              .Append(',').Append(motion.AnimateVariant)
              .Append(',').Append(motion.ExitVariant)
              .Append(',').Append(motion.HoverVariant)
              .Append(',').Append(motion.PressedVariant)
              .Append(',').Append(motion.FocusVariant);
        }

        /// <summary>
        /// Appends a vector path to the canonical form.
        /// </summary>
        /// <remarks>
        /// Every anchor and handle, because the publisher skips writing when the hash is unchanged
        /// and a path is exactly the kind of thing an author edits repeatedly. Omitting it would
        /// mean dragging a point produced no new asset - the same failure that hid binding edits
        /// before the binding fields were added here.
        ///
        /// Fill and stroke are included for the same reason: recolouring a shape is an edit.
        /// </remarks>
        private static void AppendShape(StringBuilder sb, Vector.NexVectorShape shape)
        {
            if (shape == null || shape.IsEmpty) return;

            sb.Append("|shape:").Append(shape.FillRule)
              .Append(',').Append(shape.Filled ? 1 : 0)
              .Append(',').Append(ColorText(shape.FillColor))
              .Append(',').Append(Fixed(shape.StrokeWidth))
              .Append(',').Append(ColorText(shape.StrokeColor))
              .Append(',').Append(shape.Join)
              .Append(',').Append(shape.Cap);

            for (int c = 0; c < shape.Contours.Count; c++)
            {
                var contour = shape.Contours[c];
                if (contour == null) continue;

                sb.Append("|c:").Append(contour.Closed ? 1 : 0);

                var anchors = contour.Anchors;
                for (int a = 0; a < anchors.Count; a++)
                {
                    var anchor = anchors[a];
                    sb.Append(':').Append(Fixed(anchor.Position.x)).Append(',').Append(Fixed(anchor.Position.y))
                      .Append(';').Append(Fixed(anchor.InHandle.x)).Append(',').Append(Fixed(anchor.InHandle.y))
                      .Append(';').Append(Fixed(anchor.OutHandle.x)).Append(',').Append(Fixed(anchor.OutHandle.y));
                }
            }
        }

        private static string Fixed(float value)
            => value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);

        private static string ColorText(Color c)
            => Fixed(c.r) + "," + Fixed(c.g) + "," + Fixed(c.b) + "," + Fixed(c.a);

        /// <summary>Semicolon-separated so it can sit inside a comma-separated field.</summary>
        private static string Vector4Text(Vector4 v)
            => Fixed(v.x) + ";" + Fixed(v.y) + ";" + Fixed(v.z) + ";" + Fixed(v.w);
    }
}
