using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Feature-gate extension methods for asynchronous business rules: ship a rule dark behind a flag, run it in
    /// shadow mode, ramp it to a percentage of callers and enforce it, all from the flag provider.
    /// </summary>
    public static class AsyncBusinessRuleFeatureGateExtensions
    {
        /// <summary>
        /// Wraps the async rule in a <see cref="FeatureGatedAsyncBusinessRule"/> bound to <paramref name="flag"/> whose
        /// rollout is read from the async <paramref name="provider"/> on every evaluation. Providers stored under
        /// <see cref="RuleFeatureFlags.AsyncContextKey"/> or <see cref="RuleFeatureFlags.ContextKey"/> override it for that evaluation.
        /// </summary>
        /// <param name="rule">The async rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="provider">The async provider that supplies the flag's rollout.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <returns>The gated rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/>, <paramref name="flag"/> or <paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public static FeatureGatedAsyncBusinessRule GatedBy(this IAsyncBusinessRule rule, string flag, IAsyncRuleFeatureFlagProvider provider, Func<BusinessRuleContext, string>? stableKey = null)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            return new FeatureGatedAsyncBusinessRule(rule, flag, provider, null, stableKey);
        }

        /// <summary>
        /// Wraps the async rule in a <see cref="FeatureGatedAsyncBusinessRule"/> bound to <paramref name="flag"/> whose
        /// rollout is read from a sync <paramref name="provider"/> on every evaluation. When the provider also implements
        /// <see cref="IAsyncRuleFeatureFlagProvider"/> (as <see cref="InMemoryRuleFeatureFlagProvider"/> does) the async
        /// interface is used first. Providers stored under <see cref="RuleFeatureFlags.AsyncContextKey"/> or
        /// <see cref="RuleFeatureFlags.ContextKey"/> override it for that evaluation.
        /// </summary>
        /// <typeparam name="TProvider">The provider type; inferred, and generic only so that a provider implementing both interfaces binds here without ambiguity.</typeparam>
        /// <param name="rule">The async rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="provider">The provider that supplies the flag's rollout.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <returns>The gated rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/>, <paramref name="flag"/> or <paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public static FeatureGatedAsyncBusinessRule GatedBy<TProvider>(this IAsyncBusinessRule rule, string flag, TProvider provider, Func<BusinessRuleContext, string>? stableKey = null)
            where TProvider : IRuleFeatureFlagProvider
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            IAsyncRuleFeatureFlagProvider? asyncProvider = provider as IAsyncRuleFeatureFlagProvider;
            return new FeatureGatedAsyncBusinessRule(rule, flag, asyncProvider, provider, stableKey);
        }

        /// <summary>
        /// Wraps the async rule in a <see cref="FeatureGatedAsyncBusinessRule"/> bound to <paramref name="flag"/> whose
        /// provider is supplied through <see cref="RuleFeatureFlags.AsyncContextKey"/> or <see cref="RuleFeatureFlags.ContextKey"/>
        /// at evaluation time; evaluating with a context that holds no provider throws <see cref="RuleConfigurationException"/>.
        /// </summary>
        /// <param name="rule">The async rule to gate.</param>
        /// <param name="flag">The feature flag the rule is bound to.</param>
        /// <param name="stableKey">Selects the stable key (for example a customer id) used to bucket partial rollouts; null when the flag is only ever at 0% or 100%.</param>
        /// <returns>The gated rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="flag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flag"/> is empty or white space.</exception>
        public static FeatureGatedAsyncBusinessRule GatedBy(this IAsyncBusinessRule rule, string flag, Func<BusinessRuleContext, string>? stableKey = null)
        {
            return new FeatureGatedAsyncBusinessRule(rule, flag, null, null, stableKey);
        }
    }
}
