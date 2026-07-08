namespace emiteat.NexUI.Core.Validation
{
    public enum UIValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>A single validation finding.</summary>
    public readonly struct UIValidationResult
    {
        public UIValidationSeverity Severity { get; }
        public string ValidatorId { get; }
        public string Message { get; }

        /// <summary>Optional asset / object the finding relates to.</summary>
        public UnityEngine.Object Target { get; }

        public UIValidationResult(UIValidationSeverity severity, string validatorId, string message, UnityEngine.Object target = null)
        {
            Severity = severity;
            ValidatorId = validatorId;
            Message = message;
            Target = target;
        }

        public static UIValidationResult Info(string id, string msg, UnityEngine.Object target = null)
            => new UIValidationResult(UIValidationSeverity.Info, id, msg, target);

        public static UIValidationResult Warning(string id, string msg, UnityEngine.Object target = null)
            => new UIValidationResult(UIValidationSeverity.Warning, id, msg, target);

        public static UIValidationResult Error(string id, string msg, UnityEngine.Object target = null)
            => new UIValidationResult(UIValidationSeverity.Error, id, msg, target);
    }
}
