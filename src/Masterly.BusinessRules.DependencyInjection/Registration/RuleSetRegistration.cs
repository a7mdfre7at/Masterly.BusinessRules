using System;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// One <c>AddRuleSet</c> call recorded by <see cref="BusinessRuleOptions"/> and stored in the service collection, so
    /// that several <c>AddBusinessRules</c> calls contribute layers to the single <see cref="IRuleRegistry"/> singleton
    /// in registration order. The configuration callback runs when the registry is first resolved and receives the
    /// root <see cref="IServiceProvider"/>, so factories can capture resolved services.
    /// </summary>
    internal sealed class RuleSetRegistration
    {
        public RuleSetRegistration(string name, RuleScope scope, Action<IServiceProvider, RuleSetBuilder> configure)
        {
            Name = name;
            Scope = scope;
            Configure = configure;
        }

        /// <summary>
        /// Gets the set name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the scope the layer applies to.
        /// </summary>
        public RuleScope Scope { get; }

        /// <summary>
        /// Gets the callback that configures the layer.
        /// </summary>
        public Action<IServiceProvider, RuleSetBuilder> Configure { get; }

        /// <summary>
        /// Declares this layer on the builder, running the callback with the provider.
        /// </summary>
        /// <param name="builder">The registry builder.</param>
        /// <param name="provider">The service provider handed to the callback.</param>
        public void ApplyTo(RuleRegistryBuilder builder, IServiceProvider provider)
        {
            builder.AddSet(Name, Scope, set => Configure(provider, set));
        }
    }
}
