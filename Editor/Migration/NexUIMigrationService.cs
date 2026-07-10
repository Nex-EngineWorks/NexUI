using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace emiteat.NexUI.Editor.Migration
{
    /// <summary>E1: applies selected <see cref="NexUIMigrationHit"/>s as literal text replacements, backing up each file first.</summary>
    public static class NexUIMigrationService
    {
        /// <summary>Writes each affected file's replaced text, leaving a ".bak" of the original next to it. Returns the number of files changed.</summary>
        public static int Apply(IEnumerable<NexUIMigrationHit> hits)
        {
            var changed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var hit in hits)
                {
                    if (!hit.Selected) continue;

                    string text;
                    try
                    {
                        text = File.ReadAllText(hit.AbsolutePath);
                    }
                    catch
                    {
                        continue;
                    }

                    var original = text;
                    foreach (var rule in hit.OccurrencesByRule.Keys)
                        text = text.Replace(rule.OldToken, rule.NewToken);

                    if (text == original) continue;

                    File.WriteAllText(hit.AbsolutePath + ".bak", original);
                    File.WriteAllText(hit.AbsolutePath, text);
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
            return changed;
        }
    }
}
