using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// One frozen <c>AddSet</c> call: the set name, the <see cref="RuleScope"/> the layer applies to, its position in
    /// declaration order (the tie-break between equally specific layers), the sync and async rule factories it adds and
    /// the codes it disables. Built by <see cref="RuleSetBuilder"/>, read by <see cref="RuleRegistry"/>.
    /// </summary>
    internal sealed class RuleSetLayer
    {
        public RuleSetLayer(
            string name,
            RuleScope scope,
            int order,
            IReadOnlyList<Func<BusinessRuleContext, IBusinessRule>> factories,
            IReadOnlyList<Func<BusinessRuleContext, IAsyncBusinessRule>> asyncFactories,
            IReadOnlyCollection<string> disabledCodes)
        {
            Name = name;
            Scope = scope;
            Order = order;
            Factories = factories;
            AsyncFactories = asyncFactories;
            DisabledCodes = disabledCodes;
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
        /// Gets the declaration index across the whole registry.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Gets the sync rule factories, in the order they were added.
        /// </summary>
        public IReadOnlyList<Func<BusinessRuleContext, IBusinessRule>> Factories { get; }

        /// <summary>
        /// Gets the async rule factories, in the order they were added.
        /// </summary>
        public IReadOnlyList<Func<BusinessRuleContext, IAsyncBusinessRule>> AsyncFactories { get; }

        /// <summary>
        /// Gets the codes this layer disables in the layers resolved before it.
        /// </summary>
        public IReadOnlyCollection<string> DisabledCodes { get; }
    }
}
