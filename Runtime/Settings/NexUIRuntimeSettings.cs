using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Command;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Settings
{
    /// <summary>
    /// Applies a <see cref="NexUISettings"/> asset onto a live <see cref="UIManager"/>:
    /// registers screens, themes and motions, and wires the motion player/resolver and
    /// optional command log. Backend factories / layer roots are still provided by the
    /// per-backend Integration bootstrap.
    /// </summary>
    public static class NexUIRuntimeSettings
    {
        /// <summary>The command log created when <c>enableCommandLog</c> is set (else null).</summary>
        public static CommandLog CommandLog { get; private set; }

        public static void Apply(UIManager manager, NexUISettings settings)
        {
            if (manager == null || settings == null) return;

            foreach (var screen in settings.screens)
                if (screen != null) manager.RegisterScreen(screen);

            foreach (var theme in settings.themes)
                if (theme != null) NexUITheme.Registry.Register(theme);

            if (settings.useBuiltInMotionPlayer)
            {
                manager.MotionPlayer ??= new BuiltInMotionPlayer();
                manager.MotionResolver ??= new MotionResolver();
            }

            if (settings.enableCommandLog)
                CommandLog = new CommandLog();
        }

        // ---- Auto bootstrap -------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            var settings = NexUISettingsProvider.Current;
            if (settings == null) return;
            if (settings.bootstrapMode != NexUIBootstrapMode.RuntimeInitializeOnLoad) return;

            // Note: bare 'NexUI' would resolve to the namespace here; qualify with Core.
            Apply(Core.NexUI.Manager, settings);
        }
    }
}
