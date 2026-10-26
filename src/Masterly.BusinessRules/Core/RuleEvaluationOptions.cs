namespace Masterly.BusinessRules
{
    /// <summary>
    /// Options for the batch methods of <see cref="BusinessRuleChecker"/> that take an options object
    /// instead of boolean parameters.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules, context, new RuleEvaluationOptions
    /// {
    ///     OnFault = FaultPolicy.TreatAsBroken,
    ///     Observer = new LoggingObserver()
    /// });
    ///
    /// BusinessRuleChecker.CheckAll(rules, context, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });
    /// </code>
    /// </example>
    public sealed class RuleEvaluationOptions
    {
        /// <summary>
        /// Gets or sets whether the batch stops at the first broken rule. In <c>CheckAll</c> only a rule at or above
        /// <see cref="ThrowOn"/> stops the batch; rules after the stop are recorded as <see cref="RuleOutcome.NotEvaluated"/>.
        /// Defaults to false.
        /// </summary>
        public bool StopOnFirstFailure { get; set; }

        /// <summary>
        /// Gets or sets what happens when a rule throws. Defaults to <see cref="FaultPolicy.Throw"/>.
        /// </summary>
        public FaultPolicy OnFault { get; set; } = FaultPolicy.Throw;

        /// <summary>
        /// Gets or sets the lowest severity that makes <c>CheckAll</c> throw. Broken rules below the threshold are
        /// not thrown but are reachable through <see cref="BusinessRuleValidationException.CollectedRules"/> when the
        /// batch does throw. Ignored by <c>EvaluateAll</c>, which never throws for broken rules.
        /// Defaults to <see cref="RuleSeverity.Info"/>, meaning every broken rule throws.
        /// </summary>
        public RuleSeverity ThrowOn { get; set; } = RuleSeverity.Info;

        /// <summary>
        /// Gets or sets the observer that receives per-rule and batch lifecycle callbacks from both
        /// <c>CheckAll</c> and <c>EvaluateAll</c>. Defaults to null.
        /// </summary>
        public IRuleExecutionObserver? Observer { get; set; }
    }
}
