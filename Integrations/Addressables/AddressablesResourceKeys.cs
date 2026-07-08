#if NEXUI_HAS_ADDRESSABLES
namespace emiteat.NexUI.Integrations.Addressables
{
    /// <summary>Well-known Addressables key helpers for NexUI content (optional conventions).</summary>
    public static class AddressablesResourceKeys
    {
        public const string ScreenPrefix = "nexui.screen.";
        public const string MotionPrefix = "nexui.motion.";
        public const string ThemePrefix = "nexui.theme.";

        public static string Screen(string id) => ScreenPrefix + id;
        public static string Motion(string id) => MotionPrefix + id;
        public static string Theme(string id) => ThemePrefix + id;
    }
}
#endif
