using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Editor.Migration
{
    /// <summary>
    /// E1: migration wizard UI. Scans the project for known breaking renames and lets the
    /// user review/select which files to auto-fix, instead of hand-editing scene/prefab YAML
    /// or deleting/re-importing folders after a major-version update.
    /// </summary>
    public sealed class NexUIMigrationWindow : EditorWindow
    {
        private List<NexUIMigrationHit> _hits;
        private Vector2 _scroll;
        private string _lastResult;

        public static void Open() => GetWindow<NexUIMigrationWindow>("NexUI Migration");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("NexUI Migration Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scans Assets/ for known breaking renames from older NexUI versions (namespaces, " +
                "package ids) and can rewrite them in place. Each changed file gets a .bak backup " +
                "next to it before it is modified.",
                MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button("Scan Project", GUILayout.Height(28)))
            {
                _hits = NexUIMigrationScanner.Scan(NexUIMigrationRules.All);
                _lastResult = null;
            }

            if (_hits == null) return;

            EditorGUILayout.Space();
            if (_hits.Count == 0)
            {
                EditorGUILayout.HelpBox("No legacy references found.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{_hits.Count} file(s) with legacy references", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All")) SetAll(true);
            if (GUILayout.Button("Select None")) SetAll(false);
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            foreach (var hit in _hits)
            {
                EditorGUILayout.BeginHorizontal();
                hit.Selected = EditorGUILayout.ToggleLeft($"{hit.AssetPath}  ({hit.TotalOccurrences}x)", hit.Selected);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!HasSelection()))
            {
                if (GUILayout.Button("Apply Selected Fixes", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Apply NexUI Migration Fixes",
                        "This rewrites the selected files in place (a .bak backup of each original is kept alongside it). Continue?",
                        "Apply", "Cancel"))
                    {
                        var changed = NexUIMigrationService.Apply(_hits);
                        _lastResult = $"Updated {changed} file(s).";
                        _hits = NexUIMigrationScanner.Scan(NexUIMigrationRules.All);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_lastResult))
                EditorGUILayout.HelpBox(_lastResult, MessageType.Info);
        }

        private bool HasSelection()
        {
            if (_hits == null) return false;
            foreach (var hit in _hits)
                if (hit.Selected) return true;
            return false;
        }

        private void SetAll(bool value)
        {
            if (_hits == null) return;
            foreach (var hit in _hits) hit.Selected = value;
        }
    }
}
