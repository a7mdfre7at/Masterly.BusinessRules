namespace Masterly.BusinessRules
{
    /// <summary>
    /// Represents the result of a broken business rule evaluation.
    /// This immutable class contains the code, message, and severity of the rule violation.
    /// </summary>
    public sealed class BusinessRuleResult
    {
        /// <summary>
        /// Gets the unique code identifying the broken rule.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the human-readable message describing the rule violation.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the severity level of the rule violation.
        /// </summary>
        public RuleSeverity Severity { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleResult"/> class.
        /// </summary>
        /// <param name="code">The unique code identifying the broken rule.</param>
        /// <param name="message">The human-readable message describing the rule violation.</param>
        /// <param name="severity">The severity level of the rule violation.</param>
        public BusinessRuleResult(string code, string message, RuleSeverity severity)
        {
            Code = code;
            Message = message;
            Severity = severity;
        }
    }
}