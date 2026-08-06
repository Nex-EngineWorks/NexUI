using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Applies authored control settings to a built uGUI object.
    /// </summary>
    /// <remarks>
    /// The single home for "what does <c>scroll.inertia</c> do to a ScrollRect". Both writers call
    /// it: the Studio's prefab writer while saving, and the compiled runtime while building. Before
    /// this existed only the prefab writer applied any of it, so the same screen behaved differently
    /// depending on which path produced it - and nothing said so.
    ///
    /// Every setting is applied only when the source reports it as authored. A control's own
    /// defaults are better than a default this file invents, and writing every property
    /// unconditionally would also make an unchanged screen produce a different prefab each save.
    ///
    /// Keys are authoring schema keys, unchanged. One vocabulary from the Inspector through the
    /// compiled asset to the runtime is what keeps three layers describing the same setting.
    /// </remarks>
    public static class NexUGuiPropertyApplier
    {
        /// <summary>Applies everything relevant to whatever components are on <paramref name="target"/>.</summary>
        public static void Apply(GameObject target, INexPropertySource source)
        {
            if (target == null || source == null) return;

            ApplyGraphic(target, source);
            ApplyInputField(target, source);
            ApplyDropdown(target, source);
            ApplyScrollRect(target, source);
            ApplyText(target, source);
        }

        private static void ApplyGraphic(GameObject target, INexPropertySource source)
        {
            var graphic = target.GetComponent<Graphic>();
            if (graphic == null) return;

            if (source.TryGetBool("media.raycastTarget", out var raycast)) graphic.raycastTarget = raycast;

            // maskable lives on MaskableGraphic, not on Graphic.
            if (source.TryGetBool("media.maskable", out var maskable) && graphic is MaskableGraphic asMaskable)
                asMaskable.maskable = maskable;
        }

        private static void ApplyInputField(GameObject target, INexPropertySource source)
        {
            var tmp = target.GetComponent<TMP_InputField>();
            if (tmp != null)
            {
                if (source.TryGetInt("input.maxLength", out var limit)) tmp.characterLimit = limit;
                if (source.TryGetBool("input.readOnly", out var readOnly)) tmp.readOnly = readOnly;
                if (source.TryGetEnumName("input.contentType", out var content))
                    tmp.contentType = TmpContentType(content);
                if (source.TryGetEnumName("input.lineType", out var line))
                    tmp.lineType = TmpLineType(line);
                return;
            }

            var legacy = target.GetComponent<InputField>();
            if (legacy == null) return;

            if (source.TryGetInt("input.maxLength", out var legacyLimit)) legacy.characterLimit = legacyLimit;
            if (source.TryGetBool("input.readOnly", out var legacyReadOnly)) legacy.readOnly = legacyReadOnly;
            if (source.TryGetEnumName("input.contentType", out var legacyContent))
                legacy.contentType = LegacyContentType(legacyContent);
            if (source.TryGetEnumName("input.lineType", out var legacyLine))
                legacy.lineType = LegacyLineType(legacyLine);
        }

        private static void ApplyDropdown(GameObject target, INexPropertySource source)
        {
            if (!source.TryGetString("choice.options", out var joined) || string.IsNullOrEmpty(joined)) return;

            var options = joined.Split('\n');

            var tmp = target.GetComponent<TMP_Dropdown>();
            if (tmp != null)
            {
                tmp.ClearOptions();
                foreach (var option in options) tmp.options.Add(new TMP_Dropdown.OptionData(option));
                tmp.RefreshShownValue();
                return;
            }

            var legacy = target.GetComponent<Dropdown>();
            if (legacy == null) return;

            legacy.ClearOptions();
            foreach (var option in options) legacy.options.Add(new Dropdown.OptionData(option));
            legacy.RefreshShownValue();
        }

        private static void ApplyScrollRect(GameObject target, INexPropertySource source)
        {
            var scroll = target.GetComponent<ScrollRect>();
            if (scroll == null) return;

            if (source.TryGetBool("scroll.horizontal", out var horizontal)) scroll.horizontal = horizontal;
            if (source.TryGetBool("scroll.vertical", out var vertical)) scroll.vertical = vertical;
            if (source.TryGetBool("scroll.inertia", out var inertia)) scroll.inertia = inertia;
            if (source.TryGetFloat("scroll.elasticity", out var elasticity)) scroll.elasticity = elasticity;
            if (source.TryGetFloat("scroll.decelerationRate", out var deceleration))
                scroll.decelerationRate = deceleration;
            if (source.TryGetFloat("scroll.sensitivity", out var sensitivity))
                scroll.scrollSensitivity = sensitivity;

            if (source.TryGetEnumName("scroll.movement", out var movement))
            {
                switch (movement)
                {
                    case "Unrestricted": scroll.movementType = ScrollRect.MovementType.Unrestricted; break;
                    case "Clamped": scroll.movementType = ScrollRect.MovementType.Clamped; break;
                    default: scroll.movementType = ScrollRect.MovementType.Elastic; break;
                }
            }
        }

        private static void ApplyText(GameObject target, INexPropertySource source)
        {
            var text = target.GetComponent<TMP_Text>();
            if (text == null) return;

            if (source.TryGetBool("text.autoSize", out var autoSize)) text.enableAutoSizing = autoSize;
            if (source.TryGetFloat("text.autoSizeMin", out var min)) text.fontSizeMin = min;
            if (source.TryGetFloat("text.autoSizeMax", out var max)) text.fontSizeMax = max;
            if (source.TryGetFloat("text.characterSpacing", out var spacing)) text.characterSpacing = spacing;
            if (source.TryGetFloat("text.lineSpacing", out var lineSpacing)) text.lineSpacing = lineSpacing;
            if (source.TryGetBool("text.richText", out var rich)) text.richText = rich;

            if (source.TryGetEnumName("text.overflow", out var overflow))
            {
                switch (overflow)
                {
                    case "Ellipsis": text.overflowMode = TextOverflowModes.Ellipsis; break;
                    case "Truncate": text.overflowMode = TextOverflowModes.Truncate; break;
                    case "Masking": text.overflowMode = TextOverflowModes.Masking; break;
                    default: text.overflowMode = TextOverflowModes.Overflow; break;
                }
            }
        }

        // ---- enum mapping ----------------------------------------------------
        // By member name rather than by index: an index silently means something else the moment
        // Unity inserts a member, and authoring already stores these by name.

        private static TMP_InputField.ContentType TmpContentType(string name)
        {
            switch (name)
            {
                case "Password": return TMP_InputField.ContentType.Password;
                case "IntegerNumber": return TMP_InputField.ContentType.IntegerNumber;
                case "DecimalNumber": return TMP_InputField.ContentType.DecimalNumber;
                case "EmailAddress": return TMP_InputField.ContentType.EmailAddress;
                case "Alphanumeric": return TMP_InputField.ContentType.Alphanumeric;
                case "Name": return TMP_InputField.ContentType.Name;
                default: return TMP_InputField.ContentType.Standard;
            }
        }

        private static TMP_InputField.LineType TmpLineType(string name)
        {
            switch (name)
            {
                case "MultiLineSubmit": return TMP_InputField.LineType.MultiLineSubmit;
                case "MultiLineNewline": return TMP_InputField.LineType.MultiLineNewline;
                default: return TMP_InputField.LineType.SingleLine;
            }
        }

        private static InputField.ContentType LegacyContentType(string name)
        {
            switch (name)
            {
                case "Password": return InputField.ContentType.Password;
                case "IntegerNumber": return InputField.ContentType.IntegerNumber;
                case "DecimalNumber": return InputField.ContentType.DecimalNumber;
                case "EmailAddress": return InputField.ContentType.EmailAddress;
                case "Alphanumeric": return InputField.ContentType.Alphanumeric;
                case "Name": return InputField.ContentType.Name;
                default: return InputField.ContentType.Standard;
            }
        }

        private static InputField.LineType LegacyLineType(string name)
        {
            switch (name)
            {
                case "MultiLineSubmit": return InputField.LineType.MultiLineSubmit;
                case "MultiLineNewline": return InputField.LineType.MultiLineNewline;
                default: return InputField.LineType.SingleLine;
            }
        }
    }
}
