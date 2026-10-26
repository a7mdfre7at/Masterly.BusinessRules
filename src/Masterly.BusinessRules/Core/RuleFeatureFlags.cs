namespace Masterly.BusinessRules
{
    /// <summary>
    /// The reserved context keys of the feature-gate machinery. A provider stored under <see cref="ContextKey"/>
    /// (sync) or <see cref="AsyncContextKey"/> (async) overrides the provider injected into a gated rule for that
    /// evaluation, which lets a test or a request pipeline force a rollout without rebuilding the rule.
    /// </summary>
    /// <example>
    /// <code>
    /// BusinessRuleContext context = new BusinessRuleContext();
    /// context.Set(RuleFeatureFlags.ContextKey, RuleTestHelper.CreateFlagProvider("credit-score-v2", RuleRolloutMode.Enforced));
    /// </code>
    /// </example>
    public static class RuleFeatureFlags
    {
        /// <summary>
        /// The name of the context entry holding the sync provider override.
        /// </summary>
        public const string ProviderKeyName = "__ruleFeatureFlagProvider";

        /// <summary>
        /// The name of the context entry holding the async provider override.
        /// </summary>
        public const string AsyncProviderKeyName = "__asyncRuleFeatureFlagProvider";

        /// <summary>
        /// The typed context key under which an <see cref="IRuleFeatureFlagProvider"/> overrides the injected provider
        /// of every <see cref="FeatureGatedBusinessRule"/> (and, after the async key, every <see cref="FeatureGatedAsyncBusinessRule"/>).
        /// </summary>
        public static readonly ContextKey<IRuleFeatureFlagProvider> ContextKey = new ContextKey<IRuleFeatureFlagProvider>(ProviderKeyName);

        /// <summary>
        /// The typed context key under which an <see cref="IAsyncRuleFeatureFlagProvider"/> overrides the injected
        /// providers of every <see cref="FeatureGatedAsyncBusinessRule"/>.
        /// </summary>
        public static readonly ContextKey<IAsyncRuleFeatureFlagProvider> AsyncContextKey = new ContextKey<IAsyncRuleFeatureFlagProvider>(AsyncProviderKeyName);
    }
}
