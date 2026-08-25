using System;
using System.Collections.Generic;
using System.Reflection;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Vector;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// Guards the compiled path against the failure this repository keeps reproducing: a field is
    /// added to the authoring model and to the node program, but never to the canonical form.
    /// </summary>
    /// <remarks>
    /// The publisher skips writing when the content hash is unchanged, and the hash is computed
    /// over <see cref="NexScreenProgram.ToCanonicalString"/>. So a field the canonical form omits
    /// is a field the author can edit without the change ever reaching the compiled asset - the
    /// edit appears to work in Studio and does nothing at runtime.
    ///
    /// Hand-written per-field assertions cannot catch this, because the person who forgets the
    /// canonical form is the same person who would have written the assertion. These tests walk
    /// the field list by reflection instead, so adding a field to <see cref="NexNodeProgram"/>
    /// fails the build until the canonical form accounts for it.
    ///
    /// A field that genuinely must not affect the hash belongs in <see cref="IntentionallyUnhashed"/>
    /// with a reason, which makes the exemption a reviewed decision rather than an oversight.
    /// </remarks>
    public sealed class CompiledPathCoverageTests
    {
        /// <summary>
        /// Node program fields deliberately excluded from the canonical form, and why.
        /// </summary>
        /// <remarks>
        /// Empty today. Every field of a compiled node currently affects runtime behaviour, so
        /// every field belongs in the hash. This exists so that a future exemption has to be
        /// written down next to its reason instead of silently weakening the guard.
        /// </remarks>
        private static readonly Dictionary<string, string> IntentionallyUnhashed =
            new Dictionary<string, string>();

        [Test]
        public void EveryNodeProgramFieldChangesTheCanonicalForm()
        {
            var baseline = Canonical(Baseline());
            var missed = new List<string>();

            foreach (var field in NodeFields())
            {
                if (IntentionallyUnhashed.ContainsKey(field.Name)) continue;

                var mutated = Mutate(Baseline(), field);
                if (Canonical(mutated) == baseline) missed.Add(field.Name);
            }

            Assert.That(missed, Is.Empty,
                "These NexNodeProgram fields do not reach ToCanonicalString, so editing them " +
                "leaves the content hash unchanged and the publisher never rewrites the asset. " +
                "Add them to the canonical form, or record the exemption in IntentionallyUnhashed: "
                + string.Join(", ", missed));
        }

        /// <summary>
        /// The exemption list is only meaningful while its entries still exist.
        /// </summary>
        [Test]
        public void ExemptedFieldsStillExist()
        {
            var names = new HashSet<string>();
            foreach (var field in NodeFields()) names.Add(field.Name);

            foreach (var pair in IntentionallyUnhashed)
                Assert.That(names, Contains.Item(pair.Key),
                    "IntentionallyUnhashed lists '" + pair.Key + "' (" + pair.Value +
                    "), but NexNodeProgram no longer has that field. Remove the stale exemption.");
        }

        /// <summary>
        /// Two programs built from the same data must produce the same canonical form, otherwise
        /// the hash reports a change on every compile and the publisher rewrites unchanged assets.
        /// </summary>
        [Test]
        public void TheCanonicalFormIsStableAcrossBuilds()
            => Assert.That(Canonical(Baseline()), Is.EqualTo(Canonical(Baseline())));

        private static IEnumerable<FieldInfo> NodeFields()
            => typeof(NexNodeProgram).GetFields(BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// A node with every field at a known, non-default value, so that mutating any one of them
        /// is a real change rather than a move away from whatever Unity zero-initialised.
        /// </summary>
        private static NexNodeProgram Baseline() => new NexNodeProgram
        {
            NodeId = "node-a",
            Name = "Node A",
            ParentIndex = -1,
            Kind = NexNodeKind.Label,
            Rect = new Rect(0f, 0f, 100f, 40f),
            Anchor = NexAnchor.TopLeft,
            Tint = Color.white,
            TextColor = Color.black,
            FontSize = 14,
            Text = "hello",
            Visible = true,
            TextBindingKey = "vm.Text",
            ValueBindingKey = "vm.Value",
            VisibilityBindingKey = "vm.Visible",
            InteractableBindingKey = "vm.Interactable",
            ClassBindingKey = "vm.Class",
            TextBindingMode = State.UIBindingMode.OneWay,
            ValueBindingMode = State.UIBindingMode.OneWay,
            TextConverterKey = "upper",
            ValueConverterKey = "percent",
            CommandId = "cmd.Confirm",
            AutomationId = "auto-a",
            Role = Accessibility.AccessibilityRole.Button,
            AccessibilityLabel = "Confirm",
            FocusOrder = 0,
            Capabilities = NexNodeCapabilities.Text,
            ControlId = "control-a",
            ValueMin = 0f,
            ValueMax = 1f,
            ControlProperties = new[] { NexNodeProperty.OfText("placeholder", "type here") },
            Shape = NexShapeFactory.Rectangle(new Rect(0f, 0f, 10f, 10f)),
            Appearance = NexAppearanceProgram.Neutral
        };

        /// <summary>
        /// Returns the baseline node with one field moved to a different value of its own type.
        /// </summary>
        private static NexNodeProgram Mutate(NexNodeProgram node, FieldInfo field)
        {
            object boxed = node;
            field.SetValue(boxed, DifferentValue(field.FieldType, field.GetValue(boxed)));
            return (NexNodeProgram)boxed;
        }

        private static object DifferentValue(Type type, object current)
        {
            if (type == typeof(string)) return (string)current + "-changed";
            if (type == typeof(int)) return (int)current + 7;
            if (type == typeof(float)) return (float)current + 3.5f;
            if (type == typeof(bool)) return !(bool)current;
            // Derived from the current value, never a constant. A constant is only "different"
            // until some baseline happens to use the same one, and then the mutation is not a
            // mutation and the field silently reports as covered - which is exactly what this test
            // exists to prevent. It cost a false pass on NexLayoutProgram.Padding to learn.
            if (type == typeof(Rect))
            {
                var rect = (Rect)current;
                return new Rect(rect.x + 5f, rect.y + 6f, rect.width + 7f, rect.height + 8f);
            }
            if (type == typeof(Color))
            {
                var color = (Color)current;
                return new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a * 0.5f + 0.25f);
            }
            if (type == typeof(Vector2)) return (Vector2)current + new Vector2(11f, 13f);
            if (type == typeof(NexNodeProperty[]))
                return new[] { NexNodeProperty.OfText("placeholder", "something else") };
            if (type == typeof(NexVectorShape))
                return NexShapeFactory.Rectangle(new Rect(0f, 0f, 20f, 30f));
            if (type == typeof(Vector4)) return (Vector4)current + new Vector4(1f, 2f, 3f, 4f);
            if (type == typeof(NexLayoutProgram)) return DifferentLayout((NexLayoutProgram)current);
            if (type == typeof(NexAppearanceProgram)) return DifferentAppearance((NexAppearanceProgram)current);
            if (type == typeof(NexTypographyProgram)) return DifferentTypography((NexTypographyProgram)current);
            if (type == typeof(NexStyleProgram)) return DifferentStyle((NexStyleProgram)current);
            if (type == typeof(NexMotionProgram)) return DifferentMotion((NexMotionProgram)current);
            if (type == typeof(NexNodeProperty)) return DifferentProperty((NexNodeProperty)current);
            if (type == typeof(Vector2Int)) return (Vector2Int)current + new Vector2Int(17, 19);
            if (type == typeof(string[])) return new[] { "changed-class" };
            if (type == typeof(NexTokenOverride[]))
                return new[] { new NexTokenOverride { Key = "accent", Value = "#ff0000" } };
            if (type.IsEnum) return OtherEnumValue(type, current);

            throw new NotSupportedException(
                "CompiledPathCoverageTests does not know how to vary a field of type " + type.Name +
                ". Teach DifferentValue about it so the new field is actually covered.");
        }

        /// <summary>
        /// The same guard one level down: <see cref="NexLayoutProgram"/> is a struct of its own, so
        /// a field added to it can be missed by the canonical form exactly like a field added to
        /// the node.
        /// </summary>
        [Test]
        public void EveryLayoutProgramFieldChangesTheCanonicalForm()
        {
            var baseline = Canonical(WithLayout(BaselineLayout()));
            var missed = new List<string>();

            foreach (var field in typeof(NexLayoutProgram).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = BaselineLayout();
                field.SetValue(boxed, DifferentValue(field.FieldType, field.GetValue(boxed)));
                if (Canonical(WithLayout((NexLayoutProgram)boxed)) == baseline) missed.Add(field.Name);
            }

            Assert.That(missed, Is.Empty,
                "These NexLayoutProgram fields do not reach ToCanonicalString, so a layout edit " +
                "using them leaves the content hash unchanged: " + string.Join(", ", missed));
        }

        /// <summary>
        /// A layout is omitted from the canonical form when it is default, so a node whose layout
        /// says nothing must hash the same as one with no layout at all.
        /// </summary>
        [Test]
        public void ADefaultLayoutIsNotWrittenToTheCanonicalForm()
            => Assert.That(Canonical(WithLayout(default)), Is.EqualTo(Canonical(Baseline())));

        private static NexNodeProgram WithLayout(NexLayoutProgram layout)
        {
            var node = Baseline();
            node.Layout = layout;
            return node;
        }

        /// <summary>A layout with every field away from its default, so any one can be varied.</summary>
        private static NexLayoutProgram BaselineLayout() => new NexLayoutProgram
        {
            Mode = NexLayoutMode.Row,
            Spacing = 8f,
            Padding = new Vector4(1f, 2f, 3f, 4f),
            GridColumns = 3,
            GridCellSize = new Vector2(64f, 64f),
            Wrap = NexLayoutWrap.Wrap,
            Align = NexLayoutAlignment.Center,
            Justify = NexLayoutJustify.SpaceBetween,
            WidthSizing = NexLayoutSizing.Fill,
            HeightSizing = NexLayoutSizing.Hug,
            MinSize = new Vector2(10f, 20f),
            MaxSize = new Vector2(300f, 400f),
            Margin = new Vector4(5f, 6f, 7f, 8f),
            AspectRatio = 1.5f
        };

        /// <summary>
        /// The appearance struct needs the same one-level-down guard as the layout struct.
        /// </summary>
        [Test]
        public void EveryAppearanceProgramFieldChangesTheCanonicalForm()
        {
            var baseline = Canonical(WithAppearance(BaselineAppearance()));
            var missed = new List<string>();

            foreach (var field in typeof(NexAppearanceProgram).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = BaselineAppearance();
                field.SetValue(boxed, DifferentValue(field.FieldType, field.GetValue(boxed)));
                if (Canonical(WithAppearance((NexAppearanceProgram)boxed)) == baseline) missed.Add(field.Name);
            }

            Assert.That(missed, Is.Empty,
                "These NexAppearanceProgram fields do not reach ToCanonicalString: " + string.Join(", ", missed));
        }

        /// <summary>
        /// A neutral appearance is omitted, so a node that draws no effects hashes as it did before
        /// appearance was carried at all.
        /// </summary>
        [Test]
        public void ANeutralAppearanceIsNotWrittenToTheCanonicalForm()
            => Assert.That(Canonical(WithAppearance(NexAppearanceProgram.Neutral)),
                Is.EqualTo(Canonical(Baseline())));

        private static NexNodeProgram WithAppearance(NexAppearanceProgram appearance)
        {
            var node = Baseline();
            node.Appearance = appearance;
            return node;
        }

        /// <summary>Every appearance field away from neutral, so any one of them can be varied.</summary>
        private static NexAppearanceProgram BaselineAppearance() => new NexAppearanceProgram
        {
            Opacity = 0.5f,
            BorderWidth = 2f,
            BorderColor = Color.red,
            CornerRadius = 6f,
            DropShadow = true,
            ShadowColor = Color.blue,
            ShadowOffset = new Vector2(3f, 4f),
            ShadowBlur = 5f,
            InnerShadow = true,
            OutlineWidth = 1f,
            OutlineColor = Color.green,
            Blur = 7f,
            Mask = true,
            ImageSlice = true,
            ImageFit = NexImageFit.Cover,
            Crop = true
        };

        private static NexAppearanceProgram DifferentAppearance(NexAppearanceProgram current)
        {
            var changed = current;
            changed.CornerRadius = current.CornerRadius + 13f;
            return changed;
        }

        /// <summary>
        /// The typography struct gets the same one-level-down guard as layout and appearance.
        /// </summary>
        [Test]
        public void EveryTypographyProgramFieldChangesTheCanonicalForm()
        {
            var baseline = Canonical(WithTypography(BaselineTypography()));
            var missed = new List<string>();

            foreach (var field in typeof(NexTypographyProgram).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = BaselineTypography();
                field.SetValue(boxed, DifferentValue(field.FieldType, field.GetValue(boxed)));
                if (Canonical(WithTypography((NexTypographyProgram)boxed)) == baseline) missed.Add(field.Name);
            }

            Assert.That(missed, Is.Empty,
                "These NexTypographyProgram fields do not reach ToCanonicalString: " + string.Join(", ", missed));
        }

        /// <summary>
        /// Typography is inert until the author opens the section, so a node without overrides must
        /// hash as it did before typography was carried.
        /// </summary>
        [Test]
        public void TypographyWithoutOverridesIsNotWrittenToTheCanonicalForm()
            => Assert.That(Canonical(WithTypography(default)), Is.EqualTo(Canonical(Baseline())));

        private static NexNodeProgram WithTypography(NexTypographyProgram typography)
        {
            var node = Baseline();
            node.Typography = typography;
            return node;
        }

        private static NexTypographyProgram BaselineTypography() => new NexTypographyProgram
        {
            HasOverrides = true,
            Weight = NexFontWeight.SemiBold,
            Style = NexFontStyle.Bold | NexFontStyle.Italic,
            FontSize = 18f,
            AutoSize = true,
            MinFontSize = 9f,
            MaxFontSize = 40f,
            Alignment = NexTextAlignment.LowerRight,
            Wrapping = true,
            Overflow = NexTextOverflow.Truncate,
            Ellipsis = true,
            LineHeight = 1.4f,
            LetterSpacing = 2f,
            ParagraphSpacing = 3f,
            RichText = true,
            RightToLeft = true,
            Color = Color.cyan,
            TextShadow = true,
            ShadowColor = Color.magenta,
            ShadowOffset = new Vector2(2f, -2f),
            OutlineWidth = 1.5f,
            OutlineColor = Color.yellow
        };

        private static NexTypographyProgram DifferentTypography(NexTypographyProgram current)
        {
            var changed = current;
            changed.HasOverrides = true;
            changed.LetterSpacing = current.LetterSpacing + 17f;
            return changed;
        }

        /// <summary>Style is a struct of its own, so it needs the same field-by-field guard.</summary>
        [Test]
        public void EveryStyleProgramFieldChangesTheCanonicalForm()
            => AssertStructIsFullyHashed<NexStyleProgram>(BaselineStyle(),
                value => { var node = Baseline(); node.Style = value; return node; });

        /// <summary>And so does motion.</summary>
        [Test]
        public void EveryMotionProgramFieldChangesTheCanonicalForm()
            => AssertStructIsFullyHashed<NexMotionProgram>(BaselineMotion(),
                value => { var node = Baseline(); node.Motion = value; return node; });

        /// <summary>
        /// The screen's state table is guarded the same way the node structs are.
        /// </summary>
        /// <remarks>
        /// Separate from the node guards because a state is a screen-level fact: it hangs off the
        /// program, not off any one node, so it needs its own host. The failure mode is identical -
        /// an author edits a state, the hash does not move, and the publisher never rewrites the
        /// asset.
        /// </remarks>
        [Test]
        public void EveryStateEntryFieldChangesTheCanonicalForm()
            => AssertStructIsFullyHashed<NexStateEntry>(BaselineStates().States[0],
                value =>
                {
                    var states = BaselineStates();
                    states.States[0] = value;
                    return states;
                });

        /// <summary>And so does a single delta.</summary>
        [Test]
        public void EveryStateDeltaFieldChangesTheCanonicalForm()
            => AssertStructIsFullyHashed<NexPropertyDelta>(BaselineStates().Deltas[0],
                value =>
                {
                    var states = BaselineStates();
                    states.Deltas[0] = value;
                    return states;
                });

        /// <summary>
        /// A state table with enough distinct deltas that varying a range field stays in bounds.
        /// </summary>
        /// <remarks>
        /// <c>DeltaStart</c> and <c>DeltaCount</c> are varied by adding to them, so a one-delta
        /// baseline would index past the end. Every delta differs from its neighbours, or moving
        /// the window would produce the same text and the field would report as covered without
        /// being covered - the same false pass the constant-value rule above exists to prevent.
        /// </remarks>
        private static NexStateProgram BaselineStates()
        {
            var states = new NexStateProgram();

            for (int i = 0; i < 16; i++)
                states.Deltas.Add(new NexPropertyDelta
                {
                    NodeIndex = i,
                    Value = NexNodeProperty.OfText("text", "value-" + i)
                });

            states.States.Add(new NexStateEntry
            {
                StateId = "Selected",
                DisplayName = "Selected",
                IsDefault = true,
                DeltaStart = 0,
                DeltaCount = 1
            });

            return states;
        }

        /// <summary>
        /// The responsive table is guarded the same way, including its condition.
        /// </summary>
        /// <remarks>
        /// The condition matters as much as the deltas: a rule whose breakpoint moved applies to a
        /// different set of screens with no delta touched, so a hash that missed it would leave the
        /// published asset on the old breakpoint.
        /// </remarks>
        [Test]
        public void EveryResponsiveRuleFieldChangesTheCanonicalForm()
            => AssertStructIsFullyHashed<NexResponsiveRule>(BaselineResponsive().Rules[0],
                value =>
                {
                    var responsive = BaselineResponsive();
                    responsive.Rules[0] = value;
                    return responsive;
                });

        /// <summary>
        /// A rule that constrains no input mode must still hash its other fields.
        /// </summary>
        /// <remarks>
        /// The canonical form writes -1 for "any input mode", so <c>InputMode</c> only reaches the
        /// hash while <c>ConstrainInputMode</c> is set. The baseline therefore sets it - otherwise
        /// the guard above would report <c>InputMode</c> as uncovered and be right to.
        /// </remarks>
        private static NexResponsiveProgram BaselineResponsive()
        {
            var responsive = new NexResponsiveProgram();

            for (int i = 0; i < 16; i++)
                responsive.Deltas.Add(new NexPropertyDelta
                {
                    NodeIndex = i,
                    Value = NexNodeProperty.OfText("text", "value-" + i)
                });

            responsive.Rules.Add(new NexResponsiveRule
            {
                RuleId = "Narrow",
                MinResolution = new Vector2Int(0, 0),
                MaxResolution = new Vector2Int(1279, 9999),
                InputMode = Abstractions.UIInputMode.KeyboardMouse,
                ConstrainInputMode = true,
                DeltaStart = 0,
                DeltaCount = 1
            });

            return responsive;
        }

        private static void AssertStructIsFullyHashed<T>(T baselineValue, Func<T, NexResponsiveProgram> host)
            where T : struct
        {
            var baseline = Canonical(host(baselineValue));
            var missed = new List<string>();

            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = baselineValue;
                field.SetValue(boxed, DifferentValue(field.FieldType, field.GetValue(boxed)));
                if (Canonical(host((T)boxed)) == baseline) missed.Add(field.Name);
            }

            Assert.That(missed, Is.Empty,
                "These " + typeof(T).Name + " fields do not reach ToCanonicalString: " +
                string.Join(", ", missed));
        }

        /// <summary>
        /// The part table is guarded the same way, including its has-flags.
        /// </summary>
        /// <remarks>
        /// The flags matter as much as the values: "unset" leaves the control's own baseline alone
        /// and "set to zero" pins the part to it, and a canonical form that wrote only the value
        /// would hash the two the same.
        /// </remarks>
        [Test]
        public void EveryPartOverrideFieldChangesTheCanonicalForm()
            => AssertStructIsFullyHashed<NexPartOverride>(BaselineParts().Overrides[0],
                value =>
                {
                    var parts = BaselineParts();
                    parts.Overrides[0] = value;
                    return parts;
                });

        /// <summary>
        /// Every channel is on, so flipping a flag off is as visible as flipping one on.
        /// </summary>
        private static NexPartProgram BaselineParts()
        {
            var parts = new NexPartProgram();
            parts.Overrides.Add(new NexPartOverride
            {
                NodeIndex = 0,
                PartId = "handle",
                HasPosition = true, Position = new Vector2(4f, -2f),
                HasSizeDelta = true, SizeDelta = new Vector2(6f, 3f),
                HasRotation = true, Rotation = 15f,
                HasScale = true, Scale = new Vector2(1.5f, 0.5f),
                HasVisibility = true, Visible = true
            });
            return parts;
        }

        private static void AssertStructIsFullyHashed<T>(T baselineValue, Func<T, NexPartProgram> host)
            where T : struct
        {
            var baseline = Canonical(host(baselineValue));
            var missed = new List<string>();

            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = baselineValue;
                field.SetValue(boxed, DifferentValue(field.FieldType, field.GetValue(boxed)));
                if (Canonical(host((T)boxed)) == baseline) missed.Add(field.Name);
            }

            Assert.That(missed, Is.Empty,
                "These " + typeof(T).Name + " fields do not reach ToCanonicalString: " +
                string.Join(", ", missed));
        }

        private static string Canonical(NexPartProgram parts)
        {
            var program = ScriptableObject.CreateInstance<NexScreenProgram>();
            try
            {
                program.Initialize("screen-a", new[] { Baseline() }, null, null,
                    new Vector2(1920f, 1080f), null, null, null, null, parts);
                return program.ToCanonicalString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(program);
            }
        }

        private static string Canonical(NexResponsiveProgram responsive)
        {
            var program = ScriptableObject.CreateInstance<NexScreenProgram>();
            try
            {
                program.Initialize("screen-a", new[] { Baseline() }, null, null,
                    new Vector2(1920f, 1080f), null, null, null, responsive);
                return program.ToCanonicalString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(program);
            }
        }

        /// <summary>
        /// Walks a screen-level struct's fields the way <see cref="AssertStructIsFullyHashed{T}"/>
        /// walks a node's.
        /// </summary>
        private static void AssertStructIsFullyHashed<T>(T baselineValue, Func<T, NexStateProgram> host)
            where T : struct
        {
            var baseline = Canonical(host(baselineValue));
            var missed = new List<string>();

            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = baselineValue;
                field.SetValue(boxed, DifferentValue(field.FieldType, field.GetValue(boxed)));
                if (Canonical(host((T)boxed)) == baseline) missed.Add(field.Name);
            }

            Assert.That(missed, Is.Empty,
                "These " + typeof(T).Name + " fields do not reach ToCanonicalString: " +
                string.Join(", ", missed));
        }

        private static string Canonical(NexStateProgram states)
        {
            var program = ScriptableObject.CreateInstance<NexScreenProgram>();
            try
            {
                program.Initialize("screen-a", new[] { Baseline() }, null, null,
                    new Vector2(1920f, 1080f), null, null, states);
                return program.ToCanonicalString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(program);
            }
        }

        /// <summary>
        /// Walks a struct's fields, varies each one, and asserts the canonical form noticed.
        /// </summary>
        /// <remarks>
        /// Extracted once the third struct needed it. Each copy of this loop was identical apart
        /// from the type, and a guard that is copied is a guard that gets copied wrong.
        /// </remarks>
        private static void AssertStructIsFullyHashed<T>(T baselineValue, Func<T, NexNodeProgram> host)
            where T : struct
        {
            var baseline = Canonical(host(baselineValue));
            var missed = new List<string>();

            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = baselineValue;
                field.SetValue(boxed, DifferentValue(field.FieldType, field.GetValue(boxed)));
                if (Canonical(host((T)boxed)) == baseline) missed.Add(field.Name);
            }

            Assert.That(missed, Is.Empty,
                "These " + typeof(T).Name + " fields do not reach ToCanonicalString: " +
                string.Join(", ", missed));
        }

        private static NexStyleProgram BaselineStyle() => new NexStyleProgram
        {
            Classes = new[] { "card", "rare" },
            ThemeId = "dark",
            TokenOverrides = new[] { new NexTokenOverride { Key = "accent", Value = "#00ff00" } }
        };

        private static NexMotionProgram BaselineMotion() => new NexMotionProgram
        {
            MotionId = "motion.hover",
            InitialVariant = "in",
            AnimateVariant = "loop",
            ExitVariant = "out",
            HoverVariant = "hover",
            PressedVariant = "press",
            FocusVariant = "focus"
        };

        /// <summary>
        /// Varies both halves of a keyed property, since either one alone would leave the other
        /// unguarded.
        /// </summary>
        private static NexNodeProperty DifferentProperty(NexNodeProperty current)
        {
            var changed = current;
            changed.Key = (current.Key ?? string.Empty) + "-changed";
            changed.Text = (current.Text ?? string.Empty) + "-changed";
            return changed;
        }

        private static NexStyleProgram DifferentStyle(NexStyleProgram current)
        {
            var changed = current;
            changed.ThemeId = (current.ThemeId ?? string.Empty) + "-changed";
            return changed;
        }

        private static NexMotionProgram DifferentMotion(NexMotionProgram current)
        {
            var changed = current;
            changed.MotionId = (current.MotionId ?? string.Empty) + "-changed";
            return changed;
        }

        private static NexLayoutProgram DifferentLayout(NexLayoutProgram current)
        {
            var changed = current;
            changed.Spacing = current.Spacing + 11f;
            return changed;
        }

        /// <summary>
        /// Any declared value of the enum other than the current one. Flags enums work too, since
        /// the canonical form writes the numeric value.
        /// </summary>
        private static object OtherEnumValue(Type type, object current)
        {
            foreach (var value in Enum.GetValues(type))
                if (!Equals(value, current)) return value;

            throw new NotSupportedException(
                "Enum " + type.Name + " has no value other than " + current +
                ", so this field cannot be varied.");
        }

        private static string Canonical(NexNodeProgram node)
        {
            var program = ScriptableObject.CreateInstance<NexScreenProgram>();
            try
            {
                program.Initialize("screen-a", new[] { node }, null, null, new Vector2(1920f, 1080f), null);
                return program.ToCanonicalString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(program);
            }
        }
    }
}
