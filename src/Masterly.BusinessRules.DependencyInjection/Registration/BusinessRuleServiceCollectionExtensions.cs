using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// Registers Masterly.BusinessRules with <c>Microsoft.Extensions.DependencyInjection</c>: scans assemblies for rule
    /// types, registers the open generic <see cref="IRuleRunner{TSubject}"/> and the <see cref="IRuleRegistry"/> singleton
    /// built from every <c>AddRuleSet</c> call. Scanned rules are registered with <c>TryAddEnumerable</c>, so calling
    /// <c>AddBusinessRules</c> twice for the same assembly registers each rule once; the runner and the registry are
    /// registered with <c>TryAdd</c>, so a registration made before <c>AddBusinessRules</c> wins.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddBusinessRules(typeof(OrderMustHaveItemsRule).Assembly);
    ///
    /// services.AddBusinessRules(o => o
    ///     .AddAssemblyContaining&lt;OrderMustHaveItemsRule&gt;()
    ///     .AddRuleSet("checkout", set => set.Add(ctx => new CartMustHaveItemsRule(ctx.Get(OrderKeys.Order)))));
    ///
    /// public sealed class PlaceOrderHandler
    /// {
    ///     private readonly IRuleRunner&lt;Order&gt; _rules;
    ///     public PlaceOrderHandler(IRuleRunner&lt;Order&gt; rules) => _rules = rules;
    ///     public Task Handle(Order order, CancellationToken ct) => _rules.CheckAllAsync(order, ct);
    /// }
    /// </code>
    /// </example>
    public static class BusinessRuleServiceCollectionExtensions
    {
        /// <summary>
        /// Scans the assemblies for rule types and registers them, the <see cref="IRuleRunner{TSubject}"/> and an
        /// <see cref="IRuleRegistry"/> (empty unless another call declares rule sets), all with the default options
        /// (<see cref="ServiceLifetime.Scoped"/>, public types, untyped rules included).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assemblies">The assemblies to scan; may be empty to register only the runner and the registry.</param>
        /// <returns>The service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="assemblies"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when an assembly in <paramref name="assemblies"/> is null.</exception>
        public static IServiceCollection AddBusinessRules(this IServiceCollection services, params Assembly[] assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            return AddBusinessRules(services, options => options.AddAssemblies(assemblies));
        }

        /// <summary>
        /// Registers rule types, the <see cref="IRuleRunner{TSubject}"/> and the <see cref="IRuleRegistry"/> as configured.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configures the assemblies, filters, lifetime and rule sets.</param>
        /// <returns>The service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        public static IServiceCollection AddBusinessRules(this IServiceCollection services, Action<BusinessRuleOptions> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            BusinessRuleOptions options = new BusinessRuleOptions();
            configure(options);

            foreach (Assembly assembly in options.Assemblies)
            {
                foreach (ServiceDescriptor descriptor in RuleTypeScanner.Scan(assembly, options))
                {
                    services.TryAddEnumerable(descriptor);
                }
            }

            services.TryAdd(ServiceDescriptor.Describe(typeof(IRuleRunner<>), typeof(RuleRunner<>), options.Lifetime));

            foreach (RuleSetRegistration registration in options.RuleSets)
            {
                services.AddSingleton(registration);
            }

            services.TryAddSingleton<IRuleRegistry>(BuildRegistry);
            return services;
        }

        private static IRuleRegistry BuildRegistry(IServiceProvider provider)
        {
            RuleRegistryBuilder builder = RuleRegistry.Create();
            foreach (RuleSetRegistration registration in provider.GetServices<RuleSetRegistration>())
            {
                registration.ApplyTo(builder, provider);
            }

            return builder.Build();
        }
    }
}
