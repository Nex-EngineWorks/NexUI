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
            // The Studio rule must precede the core rule below: "com.emiteat.nexui.designer" also
            // starts with "com.emiteat.nexui", so the shorter token would rewrite the prefix first
            // and leave "com.nexengineworks.nexui.designer" - a package id that never existed.
            new NexUIMigrationRule(
                id: "emiteat-to-nexengineworks-studio-package",
                oldToken: "com.emiteat.nexui.designer",
                newToken: "com.nexengineworks.nexui.studio",
                description: "NexUI Designer became NexUI Studio: package id com.emiteat.nexui.designer → com.nexengineworks.nexui.studio.",
                introducedInVersion: "0.2.0"),
            new NexUIMigrationRule(
                id: "emiteat-to-nexengineworks-package",
                oldToken: "com.emiteat.nexui",
                newToken: "com.nexengineworks.nexui",
                description: "Package id was renamed from com.emiteat.nexui to com.nexengineworks.nexui.",
                introducedInVersion: "0.2.0"),
        };
    }
}
