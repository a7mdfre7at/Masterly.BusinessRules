using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// Collects the layers of a <see cref="RuleRegistry"/>: each <see cref="AddSet(string, RuleScope, Action{RuleSetBuilder})"/>
    /// call declares one layer of one named set for one <see cref="RuleScope"/>. The same name may be declared any
    /// number of times with different scopes (the base layer under <see cref="RuleScope.Global"/>, a tenant override, a
    /// tenant-and-region override); <see cref="Build"/> freezes everything into an immutable registry and the builder
    /// cannot be used afterwards. Obtain one with <see cref="RuleRegistry.Create"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleRegistry registry = RuleRegistry.Create()
    ///     .AddSet("checkout", set => set.Add(ctx => new CartMustHaveItemsRule(ctx.Get(OrderKeys.Order))))
    ///     .AddSet("checkout", RuleScope.Tenant("acme"), set => set.Disable("CART.EMPTY").Add(ctx => new AcmePurchaseOrderRequiredRule()))
    ///     .Build();
    /// </code>
    /// </example>
    public sealed class RuleRegistryBuilder
    {
        private const string BuiltMessage = "The rule registry has already been built; create a new builder with RuleRegistry.Create().";

        private readonly List<RuleSetLayer> _layers = new List<RuleSetLayer>();
        private bool _built;

        internal RuleRegistryBuilder()
        {
        }

        /// <summary>
        /// Gets the number of layers declared so far.
        /// </summary>
        public int LayerCount => _layers.Count;

        /// <summary>
        /// Declares the base layer of a set, applying to every scope.
        /// </summary>
        /// <param name="name">The set name. Compared ordinally.</param>
        /// <param name="configure">Adds the layer's rules and disabled codes.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        /// <exception cref="InvalidOperationException">Thrown after <see cref="Build"/>.</exception>
        public RuleRegistryBuilder AddSet(string name, Action<RuleSetBuilder> configure)
        {
            return AddSet(name, RuleScope.Global, configure);
        }

        /// <summary>
        /// Declares a layer of a set applying to the requests <paramref name="scope"/> applies to
        /// (<see cref="RuleScope.AppliesTo"/>). When a set is resolved, its applicable layers are merged from the least to
        /// the most specific, equally specific layers in declaration order; each layer first removes the codes it disables
        /// from the rules merged so far and then appends its own rules.
        /// </summary>
        /// <param name="name">The set name. Compared ordinally.</param>
        /// <param name="scope">The scope the layer applies to.</param>
        /// <param name="configure">Adds the layer's rules and disabled codes.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/>, <paramref name="scope"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        /// <exception cref="InvalidOperationException">Thrown after <see cref="Build"/>.</exception>
        public RuleRegistryBuilder AddSet(string name, RuleScope scope, Action<RuleSetBuilder> configure)
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

            RequireNotBuilt();

            RuleSetBuilder set = new RuleSetBuilder(name, scope);
            configure(set);
            _layers.Add(set.Freeze(_layers.Count));
            return this;
        }

        /// <summary>
        /// Freezes the declared layers into an immutable <see cref="RuleRegistry"/>. The builder rejects every further call.
        /// </summary>
        /// <returns>The registry.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the registry was already built.</exception>
        public RuleRegistry Build()
        {
            RequireNotBuilt();
            _built = true;
            return new RuleRegistry(_layers);
        }

        private void RequireNotBuilt()
        {
            if (_built)
            {
                throw new InvalidOperationException(BuiltMessage);
            }
        }
    }
}
