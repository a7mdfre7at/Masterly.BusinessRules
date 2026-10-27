using System;
using System.Collections.Generic;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Masterly.BusinessRules.FeatureManagement
{
    /// <summary>
    /// Settings of <see cref="FeatureManagementRuleFeatureFlagProvider"/>: how a plain enabled flag, an assigned variant
    /// and an unrecognised variant map to a <see cref="RuleRollout"/>, and how the evaluation context becomes a
    /// <c>Microsoft.FeatureManagement</c> targeting context.
    /// </summary>
    /// <example>
    /// <code>
    /// FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions
    /// {
    ///     EnabledRollout = RuleRollout.Shadow(),                      // boolean flags ship dark
    ///     OnUnrecognizedVariant = UnrecognizedVariantBehavior.Off,
    ///     TargetingContextSelector = ctx => new TargetingContext { UserId = ctx.Get&lt;string&gt;("customerId") }
    /// }.MapVariant("dark", RuleRollout.Shadow()).MapVariant("live", RuleRollout.Enforced());
    /// </code>
    /// </example>
    public sealed class FeatureManagementRolloutOptions
    {
        private readonly Dictionary<string, RuleRollout> _variantRollouts = new Dictionary<string, RuleRollout>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the rollout reported for a flag that is enabled and has no variant assigned.
        /// Defaults to <see cref="RuleRollout.Enforced(int)"/> at 100%; set <see cref="RuleRollout.Shadow(int)"/> to ship
        /// every boolean-gated rule dark. A disabled flag always reports <see cref="RuleRollout.Off"/>.
        /// </summary>
        public RuleRollout EnabledRollout { get; set; } = RuleRollout.Enforced();

        /// <summary>
        /// Gets or sets what happens when the assigned variant cannot be mapped. Defaults to <see cref="UnrecognizedVariantBehavior.Throw"/>.
        /// </summary>
        public UnrecognizedVariantBehavior OnUnrecognizedVariant { get; set; } = UnrecognizedVariantBehavior.Throw;

        /// <summary>
        /// Gets or sets the selector that builds the targeting context handed to <c>Microsoft.FeatureManagement</c>
        /// (for its <c>Microsoft.Targeting</c> filter and variant allocation) from the evaluation context, or null to
        /// let the feature manager use its own <c>ITargetingContextAccessor</c>. The selector is not called for a
        /// null evaluation context; a null result falls back to the feature manager's accessor as well.
        /// </summary>
        public Func<BusinessRuleContext, ITargetingContext?>? TargetingContextSelector { get; set; }

        /// <summary>
        /// Gets the explicit variant-name mappings consulted before the configuration and naming conventions.
        /// Names are compared case-insensitively.
        /// </summary>
        public IReadOnlyDictionary<string, RuleRollout> VariantRollouts => _variantRollouts;

        /// <summary>
        /// Maps a variant name to a rollout, replacing any previous mapping of the same name.
        /// </summary>
        /// <param name="variantName">The variant name as configured in <c>Microsoft.FeatureManagement</c>.</param>
        /// <param name="rollout">The rollout the variant stands for.</param>
        /// <returns>These options, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="variantName"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="variantName"/> is empty or white space.</exception>
        public FeatureManagementRolloutOptions MapVariant(string variantName, RuleRollout rollout)
        {
            if (variantName == null)
            {
                throw new ArgumentNullException(nameof(variantName));
            }

            if (string.IsNullOrWhiteSpace(variantName))
            {
                throw new ArgumentException("The variant name cannot be empty or white space.", nameof(variantName));
            }

            _variantRollouts[variantName] = rollout;
            return this;
        }

        /// <summary>
        /// Maps a variant name to a rollout built from a mode and a percent.
        /// </summary>
        /// <param name="variantName">The variant name as configured in <c>Microsoft.FeatureManagement</c>.</param>
        /// <param name="mode">The mode the gated rule runs in for callers inside the cohort.</param>
        /// <param name="percent">The percentage of stable keys inside the cohort. Defaults to everybody.</param>
        /// <returns>These options, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="variantName"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="variantName"/> is empty or white space.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is undefined or <paramref name="percent"/> is outside 0..100.</exception>
        public FeatureManagementRolloutOptions MapVariant(string variantName, RuleRolloutMode mode, int percent = RuleRollout.MaxPercent)
        {
            return MapVariant(variantName, new RuleRollout(mode, percent));
        }

        /// <summary>
        /// Tries to find an explicit mapping for a variant name.
        /// </summary>
        /// <param name="variantName">The variant name; null is reported as unmapped.</param>
        /// <param name="rollout">The mapped rollout, or <see cref="RuleRollout.Off"/> when unmapped.</param>
        /// <returns>True when the name is mapped.</returns>
        public bool TryGetVariantRollout(string? variantName, out RuleRollout rollout)
        {
            if (variantName == null)
            {
                rollout = RuleRollout.Off;
                return false;
            }

            return _variantRollouts.TryGetValue(variantName, out rollout);
        }
    }
}
