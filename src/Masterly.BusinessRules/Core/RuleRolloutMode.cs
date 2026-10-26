namespace Masterly.BusinessRules
{
    /// <summary>
    /// How a feature-gated rule behaves for the callers inside its rollout cohort (see <see cref="RuleRollout"/>).
    /// The mode comes from an <see cref="IRuleFeatureFlagProvider"/> at evaluation time, so moving a rule from
    /// <see cref="Shadow"/> to <see cref="Enforced"/> is a configuration flip and never touches the rule class.
    /// </summary>
    public enum RuleRolloutMode
    {
        /// <summary>
        /// The gate is closed: the inner rule is not evaluated and the gated rule passes.
        /// </summary>
        Off,

        /// <summary>
        /// The inner rule is evaluated but never blocks: the gated rule always passes, and a violation is reported
        /// through <see cref="IRuleExecutionObserver.OnShadowViolation"/> / <see cref="IAsyncRuleExecutionObserver.OnShadowViolationAsync"/>
        /// and collected on <see cref="RuleEvaluationResult.ShadowResults"/> / <see cref="AsyncRuleEvaluationResult.ShadowResults"/>.
        /// </summary>
        Shadow,

        /// <summary>
        /// The inner rule is evaluated and its verdict is reported as is.
        /// </summary>
        Enforced
    }
}
