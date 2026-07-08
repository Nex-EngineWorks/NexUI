using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Editor.ProjectSetup
{
    /// <summary>Options + orchestration for one-click project setup.</summary>
    public sealed class NexUIProjectSetupWizard
    {
        public bool createSettings = true;
        public bool createRegistries = true;
        public bool createDefaultTheme = true;
        public bool createDefaultMotion = true;
        public bool createDefaultScreens = true;

        public void Run()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                NexUIProjectAssetCreator.EnsureFolders();

                var settings = createSettings ? NexUIProjectAssetCreator.CreateSettings() : null;

                if (createRegistries)
                {
                    NexUIProjectAssetCreator.CreateScreenRegistry();
                    NexUIProjectAssetCreator.CreateMotionRegistry();
                    NexUIProjectAssetCreator.CreateThemeRegistry();
                }

                if (createDefaultTheme) NexUIProjectAssetCreator.CreateDefaultTheme();
                if (createDefaultMotion) NexUIProjectAssetCreator.CreateDefaultMotion();

                if (createDefaultScreens)
                {
                    NexUIProjectAssetCreator.CreateScreen("HUD", UILayerType.HUD, UIOpenPolicy.Single);
                    NexUIProjectAssetCreator.CreateScreen("PauseMenu", UILayerType.Modal, UIOpenPolicy.StackPush);
                }

                if (settings != null) EditorUtility.SetDirty(settings);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[NexUI] Project setup complete. See Assets/NexUI.");
        }
    }
}
