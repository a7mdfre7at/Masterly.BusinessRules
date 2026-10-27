using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// What <c>services.AddBusinessRules(o =&gt; ...)</c> registers: the assemblies to scan for rule types, how they are
    /// filtered and with which lifetime they are registered, and the layers of the scoped <see cref="IRuleRegistry"/>.
    /// Scanning registers every concrete class implementing <see cref="IBusinessRule{TSubject}"/> or
    /// <see cref="IAsyncBusinessRule{TSubject}"/> under each closed rule interface it implements (so
    /// <c>IEnumerable&lt;IBusinessRule&lt;Order&gt;&gt;</c> and <see cref="IRuleRunner{TSubject}"/> see it) and, when
    /// <see cref="IncludeUntypedRules"/> is true (it is false by default), every concrete class implementing the plain
    /// <see cref="IBusinessRule"/> or <see cref="IAsyncBusinessRule"/> under that interface. Open generic rule types and
    /// abstract classes are skipped.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddBusinessRules(o => o
    ///     .AddAssemblyContaining&lt;OrderMustHaveItemsRule&gt;()
    ///     .AddRuleSet("checkout", set => set.Add(ctx => new CartMustHaveItemsRule(ctx.Get(OrderKeys.Order))))
    ///     .AddRuleSet("checkout", RuleScope.Tenant("acme"), (sp, set) => set
    ///         .Disable("CART.EMPTY")
    ///         .Add(ctx => new OrderLimitRule(ctx.Get(OrderKeys.Order), sp.GetRequiredService&lt;IRuleParameterProvider&gt;()))));
    /// </code>
    /// </example>
    public sealed class BusinessRuleOptions
    {
        private const string NullAssemblyMessage = "The assembly at position {0} is null.";

        private readonly List<Assembly> _assemblies = new List<Assembly>();
        private readonly List<RuleSetRegistration> _ruleSets = new List<RuleSetRegistration>();

        /// <summary>
        /// Gets or sets the lifetime scanned rule types and <see cref="IRuleRunner{TSubject}"/> are registered with.
        /// Defaults to <see cref="ServiceLifetime.Scoped"/>, which works with dependencies of any lifetime; a rule whose
        /// dependencies are all singletons may use <see cref="ServiceLifetime.Singleton"/>.
        /// </summary>
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

        /// <summary>
        /// Gets or sets whether non-public (internal, private nested) rule types are registered. Defaults to false.
        /// </summary>
        public bool IncludeNonPublicTypes { get; set; }

        /// <summary>
        /// Gets or sets whether classes implementing the plain <see cref="IBusinessRule"/> / <see cref="IAsyncBusinessRule"/>
        /// are registered under those interfaces. Defaults to false: the usual plain rule captures its subject through the
        /// constructor (<c>new MinimumAgeRule(age)</c>), which the container cannot create, so registering it would fail
        /// container validation (<c>ValidateOnBuild</c>) or the first resolution of <c>IEnumerable&lt;IBusinessRule&gt;</c>.
        /// Opt in when the plain rules of the scanned assemblies take only injectable services, and exclude the rest with
        /// <see cref="TypeFilter"/>.
        /// </summary>
        public bool IncludeUntypedRules { get; set; }

        /// <summary>
        /// Gets or sets an additional predicate a candidate type must satisfy to be registered. Null (the default) accepts every candidate.
        /// </summary>
        public Func<Type, bool>? TypeFilter { get; set; }

        /// <summary>
        /// Gets the assemblies to scan, in the order they were added.
        /// </summary>
        public IReadOnlyList<Assembly> Assemblies => new ReadOnlyCollection<Assembly>(_assemblies);

        /// <summary>
        /// Gets the number of rule set layers declared.
        /// </summary>
        public int RuleSetCount => _ruleSets.Count;

        internal IReadOnlyList<RuleSetRegistration> RuleSets => _ruleSets;

        /// <summary>
        /// Adds an assembly to scan. Adding the same assembly twice scans it once.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>These options, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
        public BusinessRuleOptions AddAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            if (!_assemblies.Contains(assembly))
            {
                _assemblies.Add(assembly);
            }

            return this;
        }

        /// <summary>
        /// Adds several assemblies to scan.
        /// </summary>
        /// <param name="assemblies">The assemblies.</param>
        /// <returns>These options, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblies"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when an assembly in <paramref name="assemblies"/> is null.</exception>
        public BusinessRuleOptions AddAssemblies(params Assembly[] assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            for (int position = 0; position < assemblies.Length; position++)
            {
                if (assemblies[position] == null)
                {
                    throw new ArgumentException(string.Format(NullAssemblyMessage, position), nameof(assemblies));
                }
            }

            foreach (Assembly assembly in assemblies)
            {
                AddAssembly(assembly);
            }

            return this;
        }

        /// <summary>
        /// Adds the assembly that defines <typeparamref name="TMarker"/> to the scan.
        /// </summary>
        /// <typeparam name="TMarker">Any type from the assembly.</typeparam>
        /// <returns>These options, for chaining.</returns>
        public BusinessRuleOptions AddAssemblyContaining<TMarker>()
        {
            return AddAssembly(typeof(TMarker).Assembly);
        }

        /// <summary>
        /// Declares the base layer of a rule set in the <see cref="IRuleRegistry"/>, applying to every scope.
        /// </summary>
        /// <param name="name">The set name. Compared ordinally.</param>
        /// <param name="configure">Adds the layer's rules and disabled codes.</param>
        /// <returns>These options, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public BusinessRuleOptions AddRuleSet(string name, Action<RuleSetBuilder> configure)
        {
            return AddRuleSet(name, RuleScope.Global, configure);
        }

        /// <summary>
        /// Declares a layer of a rule set applying to the requests <paramref name="scope"/> applies to.
        /// </summary>
        /// <param name="name">The set name. Compared ordinally.</param>
        /// <param name="scope">The scope the layer applies to.</param>
        /// <param name="configure">Adds the layer's rules and disabled codes.</param>
        /// <returns>These options, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/>, <paramref name="scope"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public BusinessRuleOptions AddRuleSet(string name, RuleScope scope, Action<RuleSetBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            return AddRuleSet(name, scope, (provider, set) => configure(set));
        }

        /// <summary>
        /// Declares the base layer of a rule set whose configuration needs the container: the callback runs when the
        /// registry is first resolved and receives the root <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="name">The set name. Compared ordinally.</param>
        /// <param name="configure">Adds the layer's rules and disabled codes, with access to the provider.</param>
        /// <returns>These options, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public BusinessRuleOptions AddRuleSet(string name, Action<IServiceProvider, RuleSetBuilder> configure)
        {
            return AddRuleSet(name, RuleScope.Global, configure);
        }

        /// <summary>
        /// Declares a layer of a rule set for a scope whose configuration needs the container: the callback runs when
        /// the registry is first resolved and receives the root <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="name">The set name. Compared ordinally.</param>
        /// <param name="scope">The scope the layer applies to.</param>
        /// <param name="configure">Adds the layer's rules and disabled codes, with access to the provider.</param>
        /// <returns>These options, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/>, <paramref name="scope"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public BusinessRuleOptions AddRuleSet(string name, RuleScope scope, Action<IServiceProvider, RuleSetBuilder> configure)
        {
            RuleSetName.Require(name, nameof(name));
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            _ruleSets.Add(new RuleSetRegistration(name, scope, configure));
            return this;
        }
    }
}
