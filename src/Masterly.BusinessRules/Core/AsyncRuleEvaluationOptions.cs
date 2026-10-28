namespace Masterly.BusinessRules
{
    /// <summary>
    /// Options for the batch methods of <see cref="AsyncBusinessRuleChecker"/> that take an options object
    /// instead of boolean parameters.
    /// </summary>
    /// <remarks>
    /// Fail-fast and parallel evaluation are mutually exclusive: when <see cref="StopOnFirstFailure"/> is true the
    /// batch is evaluated sequentially even if <see cref="RunInParallel"/> is also true.
    /// <para>The class is not sealed because the checker overloads that take it constrain a type parameter to it
    /// (<c>where TOptions : AsyncRuleEvaluationOptions</c>): a bare <c>null</c> or <c>default</c> argument cannot infer
    /// that parameter, so it keeps binding to the 2.x boolean and token overloads instead of this options form.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, rules, new AsyncRuleEvaluationOptions
    /// {
    ///     RunInParallel = true,
    ///     OnFault = FaultPolicy.Record,
    ///     Observer = new TracingObserver()
    /// }, cancellationToken);
    /// </code>
    /// </example>
    public class AsyncRuleEvaluationOptions
    {
        /// <summary>
        /// Gets or sets whether the batch stops at the first broken rule. In <c>CheckAllAsync</c> only a rule at or above
        /// <see cref="ThrowOn"/> stops the batch; rules after the stop are recorded as <see cref="RuleOutcome.NotEvaluated"/>.
        /// Fail-fast wins over <see cref="RunInParallel"/>. Defaults to false.
        /// </summary>
        public bool StopOnFirstFailure { get; set; }

        /// <summary>
        /// Gets or sets whether the rules are evaluated concurrently. Ignored when <see cref="StopOnFirstFailure"/> is true.
        /// Defaults to false.
        /// </summary>
        public bool RunInParallel { get; set; }

        /// <summary>
        /// Gets or sets what happens when a rule throws. Defaults to <see cref="FaultPolicy.Throw"/>.
        /// </summary>
        public FaultPolicy OnFault { get; set; } = FaultPolicy.Throw;

        /// <summary>
        /// Gets or sets the lowest severity that makes <c>CheckAllAsync</c> throw. Broken rules below the threshold are
        /// not thrown but are reachable through <see cref="BusinessRuleValidationException.CollectedRules"/> when the
        /// batch does throw. Ignored by <c>EvaluateAllAsync</c>, which never throws for broken rules.
        /// Defaults to <see cref="RuleSeverity.Info"/>, meaning every broken rule throws.
        /// </summary>
        public RuleSeverity ThrowOn { get; set; } = RuleSeverity.Info;

        /// <summary>
        /// Gets or sets the observer that receives per-rule and batch lifecycle callbacks from both
        /// <c>CheckAllAsync</c> and <c>EvaluateAllAsync</c>. Defaults to null.
        /// </summary>
        public IAsyncRuleExecutionObserver? Observer { get; set; }

        /// <summary>
        /// Gets whether the batch will actually run in parallel: <see cref="RunInParallel"/> is requested and
        /// <see cref="StopOnFirstFailure"/> does not force sequential evaluation.
        /// </summary>
        public bool EffectiveParallel => RunInParallel && !StopOnFirstFailure;
    }
}
