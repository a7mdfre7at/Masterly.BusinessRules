using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Feature-gate extension methods for synchronous business rules: ship a rule dark behind a flag, run it in
    /// shadow mode, ramp it to a percentage of callers and enforce it, all from the flag provider.
    /// </summary>
    public static class BusinessRuleFeatureGateExtensions
    {
        /// <summary>
        /// Wraps the rule in a <see cref="FeatureGatedBusinessRule"/> bound to <paramref name="flag"/> whose rollout
        /// is read from <paramref name="provider"/> on every evaluation. A provider stored under
        /// <see cref="RuleFeatureFlags.ContextKey"/> overrides <paramref name="provider"/> for that evaluation.
        /// </summary>
        /// <param name="rule">The rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="provider">The provider that supplies the flag's rollout.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <returns>The gated rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/>, <paramref name="flag"/> or <paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public static FeatureGatedBusinessRule GatedBy(this IBusinessRule rule, string flag, IRuleFeatureFlagProvider provider, Func<BusinessRuleContext, string>? stableKey = null)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            return new FeatureGatedBusinessRule(rule, flag, provider, stableKey);
        }

        /// <summary>
        /// Wraps the rule in a <see cref="FeatureGatedBusinessRule"/> bound to <paramref name="flag"/> whose provider
        /// is supplied through <see cref="RuleFeatureFlags.ContextKey"/> at evaluation time; evaluating with a context
        /// that holds no provider throws <see cref="RuleConfigurationException"/>.
        /// </summary>
        /// <param name="rule">The rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <returns>The gated rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public static FeatureGatedBusinessRule GatedBy(this IBusinessRule rule, string flag, Func<BusinessRuleContext, string>? stableKey = null)
        {
            return new FeatureGatedBusinessRule(rule, flag, null, stableKey);
        }
    }
}
