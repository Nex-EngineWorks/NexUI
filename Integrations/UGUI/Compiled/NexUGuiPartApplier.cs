using System.Collections.Generic;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Applies compiled internal-part nudges to the parts the builder tagged.
    /// </summary>
    /// <remarks>
    /// Every value is a delta from where the control put the part, so the applier reads the current
    /// transform and adds to it rather than assigning. That is what makes the authoring model's
    /// promise true - "updating the control's layout does not rewrite every authored screen" - and
    /// it is why this has to run after the control is built rather than as part of placement.
    ///
    /// Position is flipped on Y, because the authoring model's canvas grows downward and uGUI's
    /// grows up. The flip lives here and in the prefab writer, and nowhere in between: the compiled
    /// program carries the author's numbers unchanged so that the two writers can be compared.
    /// </remarks>
    public static class NexUGuiPartApplier
    {
        public static void Apply(NexScreenProgram program, RectTransform[] built,
            NexDiagnosticBag diagnostics)
        {
            var parts = program?.Parts;
            if (parts == null || parts.IsEmpty || built == null) return;

            // One report per missing part id per screen, not per node. A grid of forty sliders
            // whose handle part the builder does not create would otherwise produce forty
            // identical warnings and bury the one sentence they are all saying.
            var reported = new HashSet<string>();

            for (int i = 0; i < parts.Overrides.Count; i++)
            {
                var over = parts.Overrides[i];
                if (over.NodeIndex < 0 || over.NodeIndex >= built.Length) continue;

                var root = built[over.NodeIndex];
                if (root == null) continue;

                var target = NexPartTag.Find(root, over.PartId);
                if (target == null)
                {
                    Report(reported, diagnostics, program.ScreenId, over.PartId);
                    continue;
                }

                if (over.HasPosition)
                    target.anchoredPosition += new Vector2(over.Position.x, -over.Position.y);
                if (over.HasSizeDelta)
                    target.sizeDelta += over.SizeDelta;
                if (over.HasRotation)
                    target.localEulerAngles += new Vector3(0f, 0f, over.Rotation);
                if (over.HasScale)
                    target.localScale = Vector3.Scale(target.localScale,
                        new Vector3(over.Scale.x, over.Scale.y, 1f));
                if (over.HasVisibility)
                    target.gameObject.SetActive(over.Visible);
            }
        }

        private static void Report(HashSet<string> reported, NexDiagnosticBag diagnostics,
            string screenId, string partId)
        {
            if (diagnostics == null || !reported.Add(partId ?? string.Empty)) return;

            diagnostics.Add(NexDiagnosticCodes.PartNotBuilt,
                new NexSourceLocation(screenId, null, null, partId),
                "A nudge targets the '" + partId + "' part, which the compiled uGUI builder does " +
                "not create - it assembles a leaner control than the prefab writer. The nudge is " +
                "in the program and applies as soon as the builder makes that part.");
        }
    }
}
