using System;

namespace Masterly.BusinessRules.FeatureManagement
{
    /// <summary>
    /// Thrown by <see cref="FeatureManagementRuleFeatureFlagProvider"/> when <c>Microsoft.FeatureManagement</c> assigns a
    /// variant that cannot be mapped to a <see cref="RuleRollout"/> and
    /// <see cref="FeatureManagementRolloutOptions.OnUnrecognizedVariant"/> is <see cref="UnrecognizedVariantBehavior.Throw"/>.
    /// The message names the flag and the variant and restates the naming convention.
    /// </summary>
    /// <example>
    /// <code>
    /// // Variant 'Big' of feature flag 'credit-score-v2' does not describe a rule rollout: ...
    /// </code>
    /// </example>
    public class RolloutVariantFormatException : FormatException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RolloutVariantFormatException"/> class.
        /// </summary>
        /// <param name="flag">The feature flag whose variant was assigned.</param>
        /// <param name="variant">The name of the variant that could not be mapped; null when the variant had no name.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        public RolloutVariantFormatException(string flag, string? variant)
            : base(BuildMessage(flag ?? throw new ArgumentNullException(nameof(flag)), variant))
        {
            Flag = flag;
            Variant = variant;
        }

        /// <summary>
        /// Gets the feature flag whose variant was assigned.
        /// </summary>
        public string Flag { get; }

        /// <summary>
        /// Gets the name of the variant that could not be mapped, or null when the variant had no name.
        /// </summary>
        public string? Variant { get; }

        private static string BuildMessage(string flag, string? variant)
        {
            string variantText = variant == null ? "<unnamed>" : $"'{variant}'";

            return $"Variant {variantText} of feature flag '{flag}' does not describe a rule rollout. " +
                $"Name the variant after a RuleRolloutMode with an optional percent ({RolloutVariantConvention.Examples}), " +
                $"give its configuration a '{RolloutVariantConvention.ModeKey}' value and an optional '{RolloutVariantConvention.PercentKey}' value, " +
                "map it explicitly in FeatureManagementRolloutOptions.VariantRollouts, or change FeatureManagementRolloutOptions.OnUnrecognizedVariant.";
        }
    }
}
