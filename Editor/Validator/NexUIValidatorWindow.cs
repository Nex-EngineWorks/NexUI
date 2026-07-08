using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Validation;
using emiteat.NexUI.Motion;
using emiteat.NexUI.Theme;

namespace emiteat.NexUI.Editor.Validator
{
    /// <summary>
    /// Editor window that runs the Core project validators plus the editor-side asset
    /// validators over the project's screen definitions and registries, and lists findings
    /// with click-to-ping.
    /// </summary>
    public sealed class NexUIValidatorWindow : EditorWindow
    {
        private UIValidationReport _report;
        private Vector2 _scroll;

        [MenuItem("Tools/NexUI/Validator")]
        public static void Open() => GetWindow<NexUIValidatorWindow>("NexUI Validator");

        private void OnGUI()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run All", GUILayout.Height(28))) RunAll();
                if (GUILayout.Button("Clear", GUILayout.Height(28))) _report = null;
            }

            if (_report == null)
            {
                EditorGUILayout.HelpBox("Run validation to inspect screens, motions and themes.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                $"Errors: {_report.ErrorCount}   Warnings: {_report.WarningCount}   Total: {_report.Results.Count}",
                EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var r in _report.Results)
            {
                var type = r.Severity switch
                {
                    UIValidationSeverity.Error => MessageType.Error,
                    UIValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox($"[{r.ValidatorId}] {r.Message}", type);
                    if (r.Target != null && GUILayout.Button("Ping", GUILayout.Width(48), GUILayout.Height(38)))
                        EditorGUIUtility.PingObject(r.Target);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunAll()
        {
            var input = GatherInput();

            // Core project validators (screen-level).
            var ctx = new UIValidationContext(input.Screens);
            _report = new ProjectValidator().Validate(ctx);

            // Editor-side asset validators.
            var editorValidators = new IProjectAssetValidator[]
            {
                new MotionPresetValidator(),
                new MotionVariantValidator(),
                new ThemeTokenValidator(),
                new RegistryValidator(),
                new CommandActionValidator(),
            };
            foreach (var v in editorValidators)
                v.Validate(input, _report);
        }

        private static NexUIValidationInput GatherInput()
        {
            return new NexUIValidationInput
            {
                Screens = LoadAll<UIScreenDefinition>("t:UIScreenDefinition"),
                Motions = FirstOrDefault<UIMotionRegistryAsset>("t:UIMotionRegistryAsset"),
                Themes = FirstOrDefault<UIThemeRegistryAsset>("t:UIThemeRegistryAsset"),
            };
        }

        private static T[] LoadAll<T>(string filter) where T : Object
        {
            var guids = AssetDatabase.FindAssets(filter);
            var list = new List<T>(guids.Length);
            foreach (var g in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                if (asset != null) list.Add(asset);
            }
            return list.ToArray();
        }

        private static T FirstOrDefault<T>(string filter) where T : Object
        {
            var all = LoadAll<T>(filter);
            return all.Length > 0 ? all[0] : null;
        }
    }
}
