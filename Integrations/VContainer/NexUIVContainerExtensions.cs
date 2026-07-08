#if NEXUI_HAS_VCONTAINER
using VContainer;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Validation;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Query;
using emiteat.NexUI.State;
using emiteat.NexUI.Theme;
using emiteat.NexUI.Settings;

namespace emiteat.NexUI.Integrations.VContainer
{
    /// <summary>
    /// Registers NexUI's runtime services into a VContainer <see cref="IContainerBuilder"/>.
    /// </summary>
    public static class NexUIVContainerExtensions
    {
        public static void RegisterNexUI(this IContainerBuilder builder, NexUISettings settings = null)
        {
            var manager = new UIManager();

            if (settings != null)
            {
                NexUIRuntimeSettings.Apply(manager, settings);
            }
            else
            {
                manager.MotionResolver ??= new MotionResolver();
                manager.MotionPlayer ??= new BuiltInMotionPlayer();
            }

            builder.RegisterInstance(manager);
            builder.RegisterInstance(manager.MotionPlayer).As<IUIMotionPlayer>();
            builder.RegisterInstance(manager.MotionResolver).As<IUIMotionResolver>();

            builder.RegisterInstance(new UIStateStore());
            builder.RegisterInstance(new UIActionResolver());

            var dispatcher = new UICommandDispatcher();
            builder.RegisterInstance(dispatcher).As<IUICommandDispatcher>().AsSelf();

            builder.RegisterInstance(NexUITheme.Registry);

            if (settings == null || settings.enableQuery)
                builder.RegisterInstance(new QueryCache());

            builder.RegisterInstance(new ProjectValidator());
        }
    }
}
#endif
