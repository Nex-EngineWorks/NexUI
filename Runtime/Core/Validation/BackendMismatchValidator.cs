namespace emiteat.NexUI.Core.Validation
{
    /// <summary>
    /// Flags relation references (opensWith / closes / parent) that point to screens
    /// declared with a different backend, which cannot share a layer surface.
    /// </summary>
    public sealed class BackendMismatchValidator : IUIValidator
    {
        public string ValidatorId => "backend-mismatch";

        public void Validate(UIValidationContext context, UIValidationReport report)
        {
            var byId = new System.Collections.Generic.Dictionary<string, UIScreenDefinition>();
            foreach (var d in context.Definitions)
                if (d != null && !string.IsNullOrEmpty(d.identity.screenId))
                    byId[d.identity.screenId] = d;

            foreach (var def in context.Definitions)
            {
                if (def == null) continue;
                CheckRelations(def, def.relations.opensWith, byId, report);
                CheckParent(def, byId, report);
            }
        }

        private void CheckRelations(UIScreenDefinition def, string[] related,
            System.Collections.Generic.Dictionary<string, UIScreenDefinition> byId, UIValidationReport report)
        {
            if (related == null) return;
            foreach (var id in related)
            {
                if (string.IsNullOrEmpty(id) || !byId.TryGetValue(id, out var other)) continue;
                if (other.backendAsset.backend != def.backendAsset.backend)
                    report.Add(UIValidationResult.Warning(ValidatorId,
                        $"'{def.identity.screenId}' opensWith '{id}' but they use different backends.", def));
            }
        }

        private void CheckParent(UIScreenDefinition def,
            System.Collections.Generic.Dictionary<string, UIScreenDefinition> byId, UIValidationReport report)
        {
            var parentId = def.relations.parentScreenId;
            if (string.IsNullOrEmpty(parentId)) return;
            if (byId.TryGetValue(parentId, out var parent) &&
                parent.backendAsset.backend != def.backendAsset.backend)
            {
                report.Add(UIValidationResult.Error(ValidatorId,
                    $"'{def.identity.screenId}' parent '{parentId}' uses a different backend.", def));
            }
        }
    }
}
