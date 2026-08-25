using System.Collections.Generic;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Diagnostics;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Reports the authored features a compiled screen carries that this backend does not act on.
    /// </summary>
    /// <remarks>
    /// The compiled program now carries motion, style classes and theme token overrides. The uGUI
    /// compiled runtime can play motion when its build options provide a registry, but still
    /// resolves no theme and has no class system. Missing runtime dependencies remain explicit.
    ///
    /// That is a deliberate intermediate state - carrying it is what lets a player be wired in
    /// later without every already-published screen missing the motion its author set - but it must
    /// not be a silent one, or "my hover animation does nothing" has no explanation anywhere.
    ///
    /// Reported once per feature per screen rather than once per node. A screen where every slot
    /// declares a hover motion would otherwise produce one warning per slot, which buries the
    /// message it is trying to deliver.
    /// </remarks>
    public static class NexUGuiCarriedFeatureReport
    {
        public static void Report(NexScreenProgram program, NexDiagnosticBag diagnostics,
            bool motionAvailable = false)
        {
            if (program == null || diagnostics == null) return;

            var reported = new HashSet<string>();
            var nodes = program.Nodes;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (!motionAvailable && !nodes[i].Motion.IsEmpty)
                    Once(reported, diagnostics, program.ScreenId, "Motion",
                        "This screen declares motion, but the compiled uGUI runtime has no motion " +
                        "player wired into it. The motion is preserved in the program and starts " +
                        "working once one is.");

                if (!string.IsNullOrEmpty(nodes[i].LocalizationKey))
                    Once(reported, diagnostics, program.ScreenId, "Localization",
                        "This screen links elements to localization keys, but the compiled uGUI " +
                        "runtime is not given a localization table to resolve them against, so the " +
                        "authored literal text is shown. The keys are preserved in the program.");

                var style = nodes[i].Style;
                if (style.Classes != null && style.Classes.Length > 0)
                    Once(reported, diagnostics, program.ScreenId, "Style Classes",
                        "This screen uses style classes. uGUI has no class system, so they are " +
                        "carried for the UI Toolkit backend and for tooling, and change nothing here.");

                if (style.TokenOverrides != null && style.TokenOverrides.Length > 0)
                    Once(reported, diagnostics, program.ScreenId, "Theme Token Overrides",
                        "This screen overrides theme tokens per element. The compiled uGUI runtime " +
                        "resolves no theme, so the overrides are carried but not applied.");
            }
        }

        private static void Once(HashSet<string> reported, NexDiagnosticBag diagnostics,
            string screenId, string feature, string detail)
        {
            if (!reported.Add(feature)) return;

            diagnostics.Add(NexDiagnosticCodes.FeatureCarriedNotApplied,
                new NexSourceLocation(screenId, null, null, feature),
                feature + " is carried by the compiled screen but not applied by the uGUI backend. " + detail);
        }
    }
}
