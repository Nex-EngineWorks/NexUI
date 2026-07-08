#if NEXUI_HAS_VCONTAINER
using VContainer;
using emiteat.NexUI.Settings;

namespace emiteat.NexUI.Integrations.VContainer
{
    /// <summary>
    /// Reusable installer so a LifetimeScope can register NexUI declaratively:
    /// <c>builder.RegisterInstaller(new NexUIVContainerInstaller(settings));</c>
    /// </summary>
    public sealed class NexUIVContainerInstaller : IInstaller
    {
        private readonly NexUISettings _settings;

        public NexUIVContainerInstaller(NexUISettings settings = null) => _settings = settings;

        public void Install(IContainerBuilder builder) => builder.RegisterNexUI(_settings);
    }
}
#endif
