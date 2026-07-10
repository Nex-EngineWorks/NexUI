using System.Collections.Generic;

namespace emiteat.NexUI.Editor.Migration
{
    /// <summary>
    /// E1: the built-in catalog of known breaking renames across NexUI releases. New major
    /// versions add a rule here rather than requiring users to hand-edit scene/prefab YAML
    /// or delete/re-import folders - the exact failure mode this wizard exists to avoid.
    /// </summary>
    public static class NexUIMigrationRules
    {
        public static IReadOnlyList<NexUIMigrationRule> All { get; } = new[]
        {
            new NexUIMigrationRule(
                id: "hyojun-to-emiteat-namespace",
                oldToken: "Hyojun.NexUI",
                newToken: "emiteat.NexUI",
                description: "Root namespace was renamed from Hyojun.NexUI to emiteat.NexUI.",
                introducedInVersion: "0.1.0"),
            new NexUIMigrationRule(
                id: "hyojun-to-emiteat-package",
                oldToken: "com.hyojun.nexui",
                newToken: "com.emiteat.nexui",
                description: "Package id was renamed from com.hyojun.nexui to com.emiteat.nexui.",
                introducedInVersion: "0.1.0"),
        };
    }
}
