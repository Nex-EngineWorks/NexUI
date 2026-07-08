using UnityEditor;

namespace emiteat.NexUI.Editor.ProjectSetup
{
    /// <summary>Menu entries for project setup.</summary>
    public static class NexUIProjectSetupMenu
    {
        [MenuItem("Tools/NexUI/Project Setup")]
        public static void OpenSetup() => NexUIProjectSetupWindow.Open();

        [MenuItem("Tools/NexUI/Quick Setup (Create Defaults)")]
        public static void QuickSetup() => new NexUIProjectSetupWizard().Run();
    }
}
