using emiteat.NexUI.Compiled;
using emiteat.NexUI.Integrations.UGUI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// Every control the compiler will emit must be one the uGUI backend can actually attach.
    /// </summary>
    /// <remarks>
    /// This exists because the two drifted apart and nothing noticed. The compiler accepted
    /// <c>Dropdown</c> and <c>InputField</c>, gave them value and text capabilities, and let their
    /// bindings compile - while <c>NexUGuiControls.Attach</c> returned null for both. A screen with
    /// a bound dropdown compiled clean, built without error, and did nothing at all.
    ///
    /// Checked by attaching to a real GameObject rather than by comparing lists, because a list
    /// comparison would pass the moment somebody adds a case that returns null.
    /// </remarks>
    public sealed class NexUGuiControlCoverageTests
    {
        private GameObject _target;

        [TearDown]
        public void TearDown()
        {
            if (_target != null) Object.DestroyImmediate(_target);
            _target = null;
        }

        private GameObject New(params System.Type[] components)
        {
            _target = new GameObject("ControlHost", components);
            return _target;
        }

        private static NexNodeProgram Node(string controlId) => new NexNodeProgram
        {
            ControlId = controlId,
            ValueMin = 0f,
            ValueMax = 1f
        };

        // ---- numeric controls -------------------------------------------------

        [Test]
        public void SliderAttaches()
        {
            var handle = NexUGuiControls.Attach(New(typeof(RectTransform)), Node("Slider"));
            Assert.IsNotNull(handle, "a slider node must get a working value handle");
            handle.Dispose();
        }

        [Test]
        public void ToggleAttaches()
        {
            var handle = NexUGuiControls.Attach(New(typeof(RectTransform)), Node("Toggle"));
            Assert.IsNotNull(handle);
            handle.Dispose();
        }

        [Test]
        public void ScrollbarAttaches()
        {
            var handle = NexUGuiControls.Attach(New(typeof(RectTransform)), Node("Scrollbar"));
            Assert.IsNotNull(handle);
            handle.Dispose();
        }

        [Test]
        public void DropdownAttachesToBothFlavours()
        {
            // Which one an element carries depends on whether the project uses TextMeshPro, and a
            // binding must not care - so both control ids have to arrive as a number.
            var legacy = NexUGuiControls.Attach(New(typeof(RectTransform), typeof(Dropdown)), Node("Dropdown"));
            Assert.IsNotNull(legacy, "the legacy dropdown must get a value handle");
            legacy.Dispose();
            Object.DestroyImmediate(_target);

            var tmp = NexUGuiControls.Attach(
                New(typeof(RectTransform), typeof(TMP_Dropdown)), Node("DropdownTMP"));
            Assert.IsNotNull(tmp, "the TMP dropdown must get a value handle");
            tmp.Dispose();
        }

        [Test]
        public void DropdownIsBuiltOnABareNode()
        {
            // The compiled path builds nodes as Panel/Image/Label/Button and never adds a dropdown,
            // so a handle that only looked for an existing one found nothing. This is the case the
            // first fix missed: it worked from a prefab and did nothing when compiled.
            var host = New(typeof(RectTransform));
            var handle = NexUGuiControls.Attach(host, Node("DropdownTMP"));

            Assert.IsNotNull(handle, "a bare node must get a dropdown built for it");
            Assert.IsNotNull(host.GetComponent<TMP_Dropdown>(), "the control itself must exist");

            var dropdown = host.GetComponent<TMP_Dropdown>();
            Assert.IsNotNull(dropdown.template,
                "without a template the dropdown accepts the click and never opens");
            Assert.IsFalse(dropdown.template.gameObject.activeSelf,
                "the template must stay inactive - uGUI clones it on open");
            Assert.IsNotNull(dropdown.captionText, "the closed state needs something to show");
            Assert.IsNotNull(dropdown.itemText, "the open list needs something to fill");

            handle.Dispose();
        }

        [Test]
        public void InputFieldIsBuiltOnABareNode()
        {
            var host = New(typeof(RectTransform));
            var handle = NexUGuiControls.AttachText(host, Node("InputFieldTMP"));

            Assert.IsNotNull(handle, "a bare node must get an input field built for it");

            var field = host.GetComponent<TMP_InputField>();
            Assert.IsNotNull(field, "the control itself must exist");
            Assert.IsNotNull(field.textComponent,
                "a field with nowhere to render accepts focus and shows nothing");
            Assert.IsNotNull(field.textViewport, "text must have something to clip against");
            Assert.IsNotNull(host.GetComponent<Graphic>(),
                "a field with no graphic cannot be hit by the raycaster and never focuses");

            handle.Dispose();
        }

        [Test]
        public void AnExistingControlIsNotDuplicated()
        {
            // A prefab-loaded screen already carries Unity's own control, which is richer than the
            // one built here. Two on one rect would fight over the same rect and the same input.
            var host = New(typeof(RectTransform), typeof(TMP_Dropdown));
            var handle = NexUGuiControls.Attach(host, Node("DropdownTMP"));

            Assert.IsNotNull(handle);
            Assert.AreEqual(1, host.GetComponents<TMP_Dropdown>().Length,
                "the existing control must be adopted, not joined by a second");
            Assert.IsNull(host.GetComponent<Dropdown>(), "and no legacy one added alongside it");

            handle.Dispose();
        }

        [Test]
        public void DropdownValueIsClampedToTheOptionsItHas()
        {
            // A bound index can outlive the list it came from. Letting uGUI snap it to zero reads
            // as "the selection was reset" rather than "that option no longer exists".
            var host = New(typeof(RectTransform), typeof(Dropdown));
            var dropdown = host.GetComponent<Dropdown>();
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("A"));
            dropdown.options.Add(new Dropdown.OptionData("B"));

            var handle = NexUGuiControls.Attach(host, Node("Dropdown"));
            Assert.IsNotNull(handle);

            handle.Value = 9f;
            Assert.AreEqual(1f, handle.Value, "an out-of-range index must clamp to the last option");

            handle.Value = -3f;
            Assert.AreEqual(0f, handle.Value, "a negative index must clamp to the first option");

            handle.Dispose();
        }

        // ---- text controls ----------------------------------------------------

        [Test]
        public void InputFieldAttachesToBothFlavours()
        {
            var legacy = NexUGuiControls.AttachText(
                New(typeof(RectTransform), typeof(InputField)), Node("InputField"));
            Assert.IsNotNull(legacy, "the legacy input field must get a text handle");
            legacy.Dispose();
            Object.DestroyImmediate(_target);

            var tmp = NexUGuiControls.AttachText(
                New(typeof(RectTransform), typeof(TMP_InputField)), Node("InputFieldTMP"));
            Assert.IsNotNull(tmp, "the TMP input field must get a text handle");
            tmp.Dispose();
        }

        [Test]
        public void ALabelGetsNoTextHandle()
        {
            // A label is written to directly and has no user edit to report, so a handle would only
            // add a subscription that never fires.
            Assert.IsNull(NexUGuiControls.AttachText(New(typeof(RectTransform)), Node("TextTMP")));
        }

        [Test]
        public void WritingTheSameTextIsSkipped()
        {
            // Assigning identical text still moves the caret to the end, so a binding echoing back
            // what was just typed would fight the user for the cursor on every keystroke.
            var host = New(typeof(RectTransform), typeof(InputField));
            var field = host.GetComponent<InputField>();

            var handle = NexUGuiControls.AttachText(host, Node("InputField"));
            Assert.IsNotNull(handle);

            handle.Text = "hello";
            Assert.AreEqual("hello", handle.Text);

            var raised = 0;
            handle.UserChanged += _ => raised++;
            handle.Text = "hello";

            Assert.AreEqual(0, raised, "re-writing the same text must not read back as a user edit");
            Assert.AreEqual("hello", field.text);

            handle.Dispose();
        }

        [Test]
        public void ABindingWriteIsNotReportedAsAUserEdit()
        {
            // The loop this prevents: write-back fires, the store updates, the watcher writes it
            // straight back, and a two-way binding feeds itself for as long as the screen is open.
            var host = New(typeof(RectTransform), typeof(InputField));

            var handle = NexUGuiControls.AttachText(host, Node("InputField"));
            Assert.IsNotNull(handle);

            var raised = 0;
            handle.UserChanged += _ => raised++;
            handle.Text = "written by a binding";

            Assert.AreEqual(0, raised, "a binding write must never look like the user typing");
            handle.Dispose();
        }

        // ---- read-only fills --------------------------------------------------

        [Test]
        public void ProgressBarGetsAFilledImage()
        {
            var host = New(typeof(RectTransform));
            var handle = NexUGuiControls.Attach(host, Node("ProgressBar"));

            Assert.IsNotNull(handle, "a progress bar must be able to receive a bound value");

            var image = host.GetComponent<Image>();
            Assert.IsNotNull(image);
            Assert.AreEqual(Image.Type.Filled, image.type, "the value has to be shown by filling");
            Assert.AreEqual(Image.FillMethod.Horizontal, image.fillMethod);

            handle.Dispose();
        }

        [Test]
        public void RadialFillFillsAroundTheCircle()
        {
            var host = New(typeof(RectTransform));
            var handle = NexUGuiControls.Attach(host, Node("RadialFill"));

            var image = host.GetComponent<Image>();
            Assert.AreEqual(Image.FillMethod.Radial360, image.fillMethod);

            handle.Dispose();
        }

        [Test]
        public void AFillNormalisesTheAuthoredRange()
        {
            // The bug this covers: ValueMin/ValueMax were hardcoded to 0 and 1 in the compiler, so
            // a bar authored 0-100 arrived normalised against the wrong range and sat at full from
            // the first value onward.
            var host = New(typeof(RectTransform));
            var node = new NexNodeProgram { ControlId = "ProgressBar", ValueMin = 0f, ValueMax = 200f };

            var handle = NexUGuiControls.Attach(host, node);
            handle.Value = 50f;

            Assert.AreEqual(0.25f, host.GetComponent<Image>().fillAmount, 1e-4f,
                "50 of 200 must fill a quarter of the bar");
            Assert.AreEqual(50f, handle.Value,
                "the handle reports the author's units, not the normalised fraction");

            handle.Dispose();
        }

        [Test]
        public void AFillClampsOutsideItsRange()
        {
            var host = New(typeof(RectTransform));
            var handle = NexUGuiControls.Attach(
                host, new NexNodeProgram { ControlId = "StatBar", ValueMin = 10f, ValueMax = 20f });

            handle.Value = 999f;
            Assert.AreEqual(1f, host.GetComponent<Image>().fillAmount, 1e-4f);

            handle.Value = -999f;
            Assert.AreEqual(0f, host.GetComponent<Image>().fillAmount, 1e-4f);

            handle.Dispose();
        }

        [Test]
        public void AFillGrowsFromTheAuthoredEdge()
        {
            var host = New(typeof(RectTransform));
            var node = new NexNodeProgram
            {
                ControlId = "ProgressBar",
                ValueMin = 0f,
                ValueMax = 1f,
                ControlProperties = new[] { NexNodeProperty.OfText("value.direction", "BottomToTop") }
            };

            var handle = NexUGuiControls.Attach(host, node);
            var image = host.GetComponent<Image>();

            Assert.AreEqual(Image.FillMethod.Vertical, image.fillMethod);
            Assert.AreEqual((int)Image.OriginVertical.Bottom, image.fillOrigin);

            handle.Dispose();
        }

        [Test]
        public void AnUnknownFillDirectionStillFills()
        {
            // A bar growing the wrong way is visibly wrong; a blank one looks like missing data,
            // which is the harder thing to diagnose.
            var host = New(typeof(RectTransform));
            var node = new NexNodeProgram
            {
                ControlId = "ProgressBar",
                ValueMin = 0f,
                ValueMax = 1f,
                ControlProperties = new[] { NexNodeProperty.OfText("value.direction", "Sideways") }
            };

            var handle = NexUGuiControls.Attach(host, node);
            Assert.AreEqual(Image.FillMethod.Horizontal, host.GetComponent<Image>().fillMethod);

            handle.Dispose();
        }

        [Test]
        public void RadialFillHonoursItsSweepDirection()
        {
            var host = New(typeof(RectTransform));
            var node = new NexNodeProgram
            {
                ControlId = "RadialFill",
                ValueMin = 0f,
                ValueMax = 1f,
                ControlProperties = new[] { NexNodeProperty.OfFlag("value.clockwise", false) }
            };

            var handle = NexUGuiControls.Attach(host, node);
            Assert.IsFalse(host.GetComponent<Image>().fillClockwise);

            handle.Dispose();
        }

        [Test]
        public void AFillNeverReportsAUserEdit()
        {
            // A read-only control that raised UserChanged would let a two-way binding write from
            // something nobody can touch - a loop with no author behind it.
            var handle = NexUGuiControls.Attach(New(typeof(RectTransform)), Node("ProgressBar"));

            var raised = 0;
            handle.UserChanged += _ => raised++;
            handle.Value = 0.5f;

            Assert.AreEqual(0, raised);
            handle.Dispose();
        }

        [Test]
        public void AnUnknownControlIdIsNotAnError()
        {
            // The compiler has already reported that the binding reaches nothing; the node must
            // still build and lay out rather than taking the screen down with it.
            Assert.IsNull(NexUGuiControls.Attach(New(typeof(RectTransform)), Node("SomethingElse")));
            Assert.IsNull(NexUGuiControls.AttachText(New(typeof(RectTransform)), Node("SomethingElse")));
        }
    }
}
