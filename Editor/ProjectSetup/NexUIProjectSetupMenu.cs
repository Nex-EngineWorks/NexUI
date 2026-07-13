using UnityEditor;

namespace emiteat.NexUI.Editor.ProjectSetup
{
    /// <summary>Menu entries for project setup.</summary>
    public static class NexUIProjectSetupMenu
    {
        public static void OpenSetup() => NexUIProjectSetupWindow.Open();

        public static void QuickSetup() => new NexUIProjectSetupWizard().Run();
    }
}
