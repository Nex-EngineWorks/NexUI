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
        public const int CurrentCompilerVersion = 5;

        [SerializeField] private int _compilerVersion = CurrentCompilerVersion;
        [SerializeField] private string _screenId;
        [SerializeField] private string _contentHash;
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private NexNodeProgram[] _nodes = new NexNodeProgram[0];
        [SerializeField] private NexSourceMap _sourceMap = new NexSourceMap();
        [SerializeField] private NexFeatureManifest _features = new NexFeatureManifest();
        [SerializeField] private NexInteractionProgram _interactions = new NexInteractionProgram();

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

        /// <summary>
        /// Fills in a freshly created instance. Only the compiler calls this - the asset is
        /// immutable once published, and the runtime has no reason to mutate it.
        /// </summary>
        public void Initialize(string screenId, NexNodeProgram[] nodes, NexSourceMap sourceMap,
            NexFeatureManifest features, Vector2 referenceResolution, string contentHash,
            NexInteractionProgram interactions = null)
        {
            _compilerVersion = CurrentCompilerVersion;
            _screenId = screenId ?? string.Empty;
            _nodes = nodes ?? new NexNodeProgram[0];
            _sourceMap = sourceMap ?? new NexSourceMap();
            _features = features ?? new NexFeatureManifest();
            _interactions = interactions ?? new NexInteractionProgram();
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
                  .Append('|').Append(n.FocusOrder)
                  .Append('|').Append((int)n.Capabilities)
                  .Append('|').Append(n.ControlId)
                  .Append('|').Append(Fixed(n.ValueMin)).Append(',').Append(Fixed(n.ValueMax));

                AppendProperties(sb, n.ControlProperties);
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

            return sb.ToString();
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
                var property = properties[i];
                sb.Append("|p:").Append(property.Key).Append('=');

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
    }
}
