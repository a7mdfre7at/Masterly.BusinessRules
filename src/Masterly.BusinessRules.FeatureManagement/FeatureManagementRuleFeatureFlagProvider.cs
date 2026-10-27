using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Masterly.BusinessRules.FeatureManagement
{
    /// <summary>
    /// An <see cref="IRuleFeatureFlagProvider"/> and <see cref="IAsyncRuleFeatureFlagProvider"/> over
    /// <c>Microsoft.FeatureManagement</c>, so feature-gated rules (<c>rule.GatedBy(flag, provider, stableKey)</c>) read
    /// their <see cref="RuleRollout"/> from the same feature flags, filters and variants as the rest of the application.
    /// </summary>
    /// <remarks>
    /// <para><b>Mapping.</b> With an <see cref="IVariantFeatureManager"/> the provider first asks for the flag's
    /// assigned variant and maps it through <see cref="FeatureManagementRolloutOptions.VariantRollouts"/>, then the
    /// variant's configuration (<see cref="RolloutVariantConvention.ModeKey"/>/<see cref="RolloutVariantConvention.PercentKey"/>),
    /// then its name (<see cref="RolloutVariantConvention"/>: <c>Shadow</c>, <c>Enforced-10</c>, <c>Off</c>). A variant that
    /// maps to nothing is handled by <see cref="FeatureManagementRolloutOptions.OnUnrecognizedVariant"/>. Without a
    /// variant, and always with a plain <see cref="IFeatureManager"/>, the flag's enabled state decides:
    /// <see cref="FeatureManagementRolloutOptions.EnabledRollout"/> (<c>Enforced</c> by default) when enabled,
    /// <see cref="RuleRollout.Off"/> when disabled or unknown.</para>
    /// <para><b>Targeting.</b> When <see cref="FeatureManagementRolloutOptions.TargetingContextSelector"/> is set the
    /// evaluation context is turned into an <see cref="ITargetingContext"/> and handed to the feature manager, so
    /// <c>Microsoft.Targeting</c> filters and percentile variant allocation see the same customer the gated rule's stable
    /// key describes.</para>
    /// <para><b>Sync side.</b> <c>Microsoft.FeatureManagement</c> is asynchronous only, so <see cref="GetRollout"/> blocks
    /// on <see cref="GetRolloutAsync"/> through <c>GetAwaiter().GetResult()</c>, exactly like <see cref="AsyncToSyncRuleAdapter"/>.
    /// Prefer async gated rules; the async decorator picks this provider's async side because it implements both interfaces.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// FeatureManagementRuleFeatureFlagProvider flags = new FeatureManagementRuleFeatureFlagProvider(variantFeatureManager);
    ///
    /// IAsyncBusinessRule gated = new SanctionsCheckRule(api)
    ///     .GatedBy("sanctions-check", flags, stableKey: ctx => ctx.Get&lt;string&gt;("customerId"));
    /// </code>
    /// </example>
    public sealed class FeatureManagementRuleFeatureFlagProvider : IRuleFeatureFlagProvider, IAsyncRuleFeatureFlagProvider
    {
        private readonly IFeatureManager? _featureManager;
        private readonly IVariantFeatureManager? _variantFeatureManager;
        private readonly FeatureManagementRolloutOptions _options;

        /// <summary>
        /// Initializes a provider over a variant-aware feature manager.
        /// </summary>
        /// <param name="variantFeatureManager">The feature manager that assigns variants and reports enabled state.</param>
        /// <param name="options">The mapping options, or null for the defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="variantFeatureManager"/> is null.</exception>
        public FeatureManagementRuleFeatureFlagProvider(IVariantFeatureManager variantFeatureManager, FeatureManagementRolloutOptions? options = null)
        {
            _variantFeatureManager = variantFeatureManager ?? throw new ArgumentNullException(nameof(variantFeatureManager));
            _options = options ?? new FeatureManagementRolloutOptions();
        }

        /// <summary>
        /// Initializes a provider over a plain feature manager; only the enabled state is consulted.
        /// </summary>
        /// <param name="featureManager">The feature manager that reports enabled state.</param>
        /// <param name="options">The mapping options, or null for the defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="featureManager"/> is null.</exception>
        public FeatureManagementRuleFeatureFlagProvider(IFeatureManager featureManager, FeatureManagementRolloutOptions? options = null)
        {
            _featureManager = featureManager ?? throw new ArgumentNullException(nameof(featureManager));
            _options = options ?? new FeatureManagementRolloutOptions();
        }

        /// <summary>
        /// Gets the mapping options in effect.
        /// </summary>
        public FeatureManagementRolloutOptions Options => _options;

        /// <summary>
        /// Gets whether the provider was built over an <see cref="IVariantFeatureManager"/>, so variants are consulted.
        /// </summary>
        public bool SupportsVariants => _variantFeatureManager != null;

        /// <inheritdoc />
        /// <remarks>Blocks on <see cref="GetRolloutAsync"/>; <c>Microsoft.FeatureManagement</c> has no synchronous API.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        /// <exception cref="RolloutVariantFormatException">Thrown when the assigned variant cannot be mapped and the options say to throw.</exception>
        public RuleRollout GetRollout(string flag, BusinessRuleContext context)
        {
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            return GetRolloutAsync(flag, context, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is null.</exception>
        /// <exception cref="RolloutVariantFormatException">Thrown when the assigned variant cannot be mapped and the options say to throw.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<RuleRollout> GetRolloutAsync(string flag, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (flag == null)
            {
                throw new ArgumentNullException(nameof(flag));
            }

            cancellationToken.ThrowIfCancellationRequested();

            ITargetingContext? targeting = ResolveTargeting(context);

            if (_variantFeatureManager != null)
            {
                Variant? variant = await GetVariantAsync(_variantFeatureManager, flag, targeting, cancellationToken).ConfigureAwait(false);

                if (variant != null)
                {
                    if (TryMapVariant(variant, out RuleRollout fromVariant))
                    {
                        return fromVariant;
                    }

                    switch (_options.OnUnrecognizedVariant)
                    {
                        case UnrecognizedVariantBehavior.Off:
                            return RuleRollout.Off;
                        case UnrecognizedVariantBehavior.UseEnabledState:
                            break;
                        default:
                            throw new RolloutVariantFormatException(flag, variant.Name);
                    }
                }
            }

            bool enabled = await IsEnabledAsync(flag, targeting, cancellationToken).ConfigureAwait(false);
            return enabled ? _options.EnabledRollout : RuleRollout.Off;
        }

        private ITargetingContext? ResolveTargeting(BusinessRuleContext? context)
        {
            Func<BusinessRuleContext, ITargetingContext?>? selector = _options.TargetingContextSelector;

            if (selector == null || context == null)
            {
                return null;
            }

            return selector(context);
        }

        private bool TryMapVariant(Variant variant, out RuleRollout rollout)
        {
            if (_options.TryGetVariantRollout(variant.Name, out rollout))
            {
                return true;
            }

            if (RolloutVariantConvention.TryRead(variant.Configuration, out rollout))
            {
                return true;
            }

            return RolloutVariantConvention.TryParse(variant.Name, out rollout);
        }

        private static async Task<Variant?> GetVariantAsync(IVariantFeatureManager manager, string flag, ITargetingContext? targeting, CancellationToken cancellationToken)
        {
            // Microsoft.FeatureManagement is nullable-oblivious; a flag without variants yields null.
            Variant? variant = targeting == null
                ? await manager.GetVariantAsync(flag, cancellationToken).ConfigureAwait(false)
                : await manager.GetVariantAsync(flag, targeting, cancellationToken).ConfigureAwait(false);

            return variant;
        }

        private async Task<bool> IsEnabledAsync(string flag, ITargetingContext? targeting, CancellationToken cancellationToken)
        {
            if (_variantFeatureManager != null)
            {
                return targeting == null
                    ? await _variantFeatureManager.IsEnabledAsync(flag, cancellationToken).ConfigureAwait(false)
                    : await _variantFeatureManager.IsEnabledAsync(flag, targeting, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            IFeatureManager manager = _featureManager!; // non-null: exactly one of the two managers is set by the constructors
            return targeting == null
                ? await manager.IsEnabledAsync(flag).ConfigureAwait(false)
                : await manager.IsEnabledAsync(flag, targeting).ConfigureAwait(false);
        }
    }
}
