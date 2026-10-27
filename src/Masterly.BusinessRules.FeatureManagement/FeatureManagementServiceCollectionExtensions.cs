using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;

namespace Masterly.BusinessRules.FeatureManagement
{
    /// <summary>
    /// Registers <see cref="FeatureManagementRuleFeatureFlagProvider"/> in an <see cref="IServiceCollection"/> so gated
    /// rules can take their flag provider from the container.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddFeatureManagement(configuration);
    /// services.AddBusinessRulesFeatureManagement(options =>
    /// {
    ///     options.TargetingContextSelector = ctx => new TargetingContext { UserId = ctx.Get&lt;string&gt;("customerId") };
    /// });
    ///
    /// IAsyncRuleFeatureFlagProvider flags = provider.GetRequiredService&lt;IAsyncRuleFeatureFlagProvider&gt;();
    /// </code>
    /// </example>
    public static class FeatureManagementServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="FeatureManagementRuleFeatureFlagProvider"/> as <see cref="IRuleFeatureFlagProvider"/> and
        /// <see cref="IAsyncRuleFeatureFlagProvider"/>, over the <see cref="IVariantFeatureManager"/> when one is registered
        /// and the <see cref="IFeatureManager"/> otherwise. Call <c>services.AddFeatureManagement(...)</c> first; the
        /// registrations use <c>TryAdd</c>, so an existing provider registration is left alone.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configures the mapping options, or null for the defaults.</param>
        /// <param name="lifetime">
        /// The lifetime of the provider. Defaults to <see cref="ServiceLifetime.Singleton"/>, matching
        /// <c>AddFeatureManagement</c>; pass <see cref="ServiceLifetime.Scoped"/> when the feature manager was registered
        /// with <c>AddScopedFeatureManagement</c>.
        /// </param>
        /// <returns>The service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lifetime"/> is not a defined <see cref="ServiceLifetime"/>.</exception>
        public static IServiceCollection AddBusinessRulesFeatureManagement(
            this IServiceCollection services,
            Action<FeatureManagementRolloutOptions>? configure = null,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (!Enum.IsDefined(typeof(ServiceLifetime), lifetime))
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "The lifetime is not a defined ServiceLifetime value.");
            }

            FeatureManagementRolloutOptions options = new FeatureManagementRolloutOptions();
            configure?.Invoke(options);

            services.TryAddSingleton(options);
            services.TryAdd(ServiceDescriptor.Describe(typeof(FeatureManagementRuleFeatureFlagProvider), CreateProvider, lifetime));
            services.TryAdd(ServiceDescriptor.Describe(typeof(IRuleFeatureFlagProvider), ResolveProvider, lifetime));
            services.TryAdd(ServiceDescriptor.Describe(typeof(IAsyncRuleFeatureFlagProvider), ResolveProvider, lifetime));

            return services;
        }

        private static object CreateProvider(IServiceProvider serviceProvider)
        {
            FeatureManagementRolloutOptions options = serviceProvider.GetService<FeatureManagementRolloutOptions>() ?? new FeatureManagementRolloutOptions();
            IVariantFeatureManager? variantFeatureManager = serviceProvider.GetService<IVariantFeatureManager>();

            if (variantFeatureManager != null)
            {
                return new FeatureManagementRuleFeatureFlagProvider(variantFeatureManager, options);
            }

            IFeatureManager? featureManager = serviceProvider.GetService<IFeatureManager>();

            if (featureManager != null)
            {
                return new FeatureManagementRuleFeatureFlagProvider(featureManager, options);
            }

            throw new InvalidOperationException(
                "No IVariantFeatureManager or IFeatureManager is registered. Call services.AddFeatureManagement(...) before AddBusinessRulesFeatureManagement().");
        }

        private static object ResolveProvider(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<FeatureManagementRuleFeatureFlagProvider>();
        }
    }
}
