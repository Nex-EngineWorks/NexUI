using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emiteat.NexUI.Core.Validation
{
    /// <summary>Accumulates validation results and summarises them.</summary>
    public sealed class UIValidationReport
    {
        private readonly List<UIValidationResult> _results = new List<UIValidationResult>();

        public IReadOnlyList<UIValidationResult> Results => _results;

        public int ErrorCount => _results.Count(r => r.Severity == UIValidationSeverity.Error);
        public int WarningCount => _results.Count(r => r.Severity == UIValidationSeverity.Warning);
        public bool HasErrors => ErrorCount > 0;

        public void Add(UIValidationResult result) => _results.Add(result);

        public void AddRange(IEnumerable<UIValidationResult> results) => _results.AddRange(results);

        public string ToSummaryString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[NexUI] Validation: {ErrorCount} error(s), {WarningCount} warning(s).");
            foreach (var r in _results)
                sb.AppendLine($"  [{r.Severity}] ({r.ValidatorId}) {r.Message}");
            return sb.ToString();
        }
    }
}
