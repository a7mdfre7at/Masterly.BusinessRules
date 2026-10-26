namespace Masterly.BusinessRules
{
    /// <summary>
    /// Supplies the <see cref="RuleRollout"/> of a feature flag to synchronous feature-gated rules
    /// (<see cref="FeatureGatedBusinessRule"/>). The rollout is read on every evaluation, so a change in the
    /// provider's configuration takes effect on the next call without rebuilding the rule.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class ConfigurationFlagProvider : IRuleFeatureFlagProvider
    /// {
    ///     public RuleRollout GetRollout(string flag, BusinessRuleContext context)
    ///         => _settings.TryGetValue(flag, out RolloutSetting s) ? new RuleRollout(s.Mode, s.Percent) : RuleRollout.Off;
    /// }
    /// </code>
    /// </example>
    public interface IRuleFeatureFlagProvider
    {
        /// <summary>
        /// Returns the rollout of <paramref name="flag"/>. An unknown flag should be reported as <see cref="RuleRollout.Off"/>.
        /// </summary>
        /// <param name="flag">The feature flag the gated rule is bound to.</param>
        /// <param name="context">The context of the evaluation; providers may use it for targeting.</param>
        /// <returns>The rollout of the flag.</returns>
        RuleRollout GetRollout(string flag, BusinessRuleContext context);
    }
}
