namespace Masterly.BusinessRules.FeatureManagement
{
    /// <summary>
    /// What <see cref="FeatureManagementRuleFeatureFlagProvider"/> does when <c>Microsoft.FeatureManagement</c> assigns a
    /// variant that does not map to a <see cref="RuleRollout"/>: it is not listed in
    /// <see cref="FeatureManagementRolloutOptions.VariantRollouts"/>, its configuration carries no valid
    /// <c>Mode</c>/<c>Percent</c> pair and its name does not follow <see cref="RolloutVariantConvention"/>.
    /// </summary>
    public enum UnrecognizedVariantBehavior
    {
        /// <summary>
        /// Throw <see cref="RolloutVariantFormatException"/> so the misconfiguration is loud. This is the default.
        /// </summary>
        Throw,

        /// <summary>
        /// Report <see cref="RuleRollout.Off"/>: the gated rule passes without being evaluated.
        /// </summary>
        Off,

        /// <summary>
        /// Ignore the variant and fall back to the flag's enabled state, as if the flag had no variants:
        /// <see cref="FeatureManagementRolloutOptions.EnabledRollout"/> when enabled, <see cref="RuleRollout.Off"/> otherwise.
        /// </summary>
        UseEnabledState
    }
}
