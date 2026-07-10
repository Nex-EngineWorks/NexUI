using UnityEditor;

namespace emiteat.NexUI.Editor.Migration
{
    /// <summary>Menu entry for the E1 migration wizard.</summary>
    public static class NexUIMigrationMenu
    {
        [MenuItem("Tools/NexUI/Migration Wizard")]
        public static void Open() => NexUIMigrationWindow.Open();
    }
}
