namespace Mosaic.Bridge.Core.Pipeline.Validation
{
    /// <summary>
    /// Severity of a validation finding.
    /// Warning = non-blocking (added to context.Warnings). Error = blocks execution.
    /// </summary>
    public enum ValidationSeverity
    {
        Warning,
        Error
    }

    /// <summary>
    /// Result of a single validation rule evaluation.
    /// </summary>
    public sealed class ValidationResult
    {
        public bool IsValid { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string Message { get; set; }

        public static ValidationResult Pass() =>
            new ValidationResult { IsValid = true };

        public static ValidationResult Warn(string message) =>
            new ValidationResult { IsValid = true, Severity = ValidationSeverity.Warning, Message = message };

        public static ValidationResult Reject(string message) =>
            new ValidationResult { IsValid = false, Severity = ValidationSeverity.Error, Message = message };
    }

    /// <summary>
    /// A rule that validates tool parameters before execution.
    /// Rules are grouped by category and evaluated by <see cref="Stages.SemanticValidatorStage"/>.
    /// </summary>
    public interface IValidationRule
    {
        /// <summary>
        /// Tool category this rule applies to (e.g., "gameobject", "material", "component").
        /// Null means the rule applies to all tools.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Evaluate the rule against the given execution context.
        /// </summary>
        ValidationResult Validate(ExecutionContext context);
    }
}
