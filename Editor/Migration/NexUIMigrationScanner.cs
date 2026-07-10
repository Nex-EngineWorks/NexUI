using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace emiteat.NexUI.Editor.Migration
{
    /// <summary>
    /// E1: walks the user's <c>Assets/</c> folder for text-based files (scripts, scenes,
    /// prefabs, ScriptableObject assets, UXML/USS, asmdef) that still reference a token from
    /// <see cref="NexUIMigrationRules.All"/>, so a major-version rename never requires the user
    /// to manually hunt through scene/prefab YAML or delete/re-import folders themselves.
    /// </summary>
    public static class NexUIMigrationScanner
    {
        private static readonly string[] ScannableExtensions =
        {
            ".cs", ".unity", ".prefab", ".asset", ".uxml", ".uss", ".asmdef", ".json"
        };

        public static List<NexUIMigrationHit> Scan(IReadOnlyList<NexUIMigrationRule> rules)
        {
            var hits = new List<NexUIMigrationHit>();
            var assetsRoot = Application.dataPath;
            if (!Directory.Exists(assetsRoot)) return hits;

            foreach (var absolutePath in Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(absolutePath);
                var isScannable = false;
                foreach (var candidate in ScannableExtensions)
                {
                    if (ext == candidate) { isScannable = true; break; }
                }
                if (!isScannable) continue;

                string text;
                try
                {
                    text = File.ReadAllText(absolutePath);
                }
                catch
                {
                    continue; // unreadable/locked file - skip rather than fail the whole scan
                }

                NexUIMigrationHit hit = null;
                foreach (var rule in rules)
                {
                    var count = CountOccurrences(text, rule.OldToken);
                    if (count <= 0) continue;

                    if (hit == null)
                    {
                        var assetPath = "Assets" + absolutePath.Substring(assetsRoot.Length).Replace('\\', '/');
                        hit = new NexUIMigrationHit(assetPath, absolutePath);
                    }
                    hit.OccurrencesByRule[rule] = count;
                }
                if (hit != null) hits.Add(hit);
            }

            return hits;
        }

        private static int CountOccurrences(string text, string token)
        {
            if (string.IsNullOrEmpty(token)) return 0;
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(token, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }
    }
}
