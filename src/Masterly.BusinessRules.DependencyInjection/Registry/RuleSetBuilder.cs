using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// Configures one layer of a rule set inside an <c>AddSet</c> call: the factories that create the layer's rules and
    /// the codes it disables. Factories, not instances, because class-based rules capture their subject in the
    /// constructor and must be created per evaluation from the context; a stateless rule can still be added as an
    /// instance with <see cref="Add(IBusinessRule)"/>. Sync and async rules are independent lists: <c>Resolve</c> /
    /// <c>CheckAll</c> see the sync list, <c>ResolveAsyncRules</c> / <c>CheckAllAsync</c> the async list (add a sync
    /// rule to the async list with <c>rule.ToAsync()</c>). A disabled code removes the rules with that code from every
    /// layer resolved before this one; the layer's own additions survive, which is what <see cref="Replace"/> relies on.
    /// The builder is frozen once its <c>AddSet</c> call returns.
    /// </summary>
    /// <example>
    /// <code>
    /// set.Add(ctx => new CartMustHaveItemsRule(ctx.Get(OrderKeys.Order)))
    ///    .Add(ctx => new OrderLimitRule(ctx.Get(OrderKeys.Order), parameters))
    ///    .AddAsyncRule(ctx => new SanctionsCheckRule(ctx.Get(OrderKeys.Order), sanctionsApi))
    ///    .Disable("CART.EMPTY");
    /// </code>
    /// </example>
    public sealed class RuleSetBuilder
    {
        private const string EmptyCodeMessage = "A rule code must not be empty or white space.";
        private const string FrozenMessage = "The rule set builder is frozen: its AddSet call has returned. Configure the set inside the callback.";

        private readonly List<Func<BusinessRuleContext, IBusinessRule>> _factories = new List<Func<BusinessRuleContext, IBusinessRule>>();
        private readonly List<Func<BusinessRuleContext, IAsyncBusinessRule>> _asyncFactories = new List<Func<BusinessRuleContext, IAsyncBusinessRule>>();
        private readonly HashSet<string> _disabledCodes = new HashSet<string>(StringComparer.Ordinal);
        private bool _frozen;

        internal RuleSetBuilder(string name, RuleScope scope)
        {
            Name = name;
            Scope = scope;
        }

        /// <summary>
        /// Gets the name of the set this layer belongs to.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the scope this layer applies to; <see cref="RuleScope.Global"/> for the base layer.
        /// </summary>
        public RuleScope Scope { get; }

        /// <summary>
        /// Gets the number of sync rule factories added so far.
        /// </summary>
        public int RuleCount => _factories.Count;

        /// <summary>
        /// Gets the number of async rule factories added so far.
        /// </summary>
        public int AsyncRuleCount => _asyncFactories.Count;

        /// <summary>
        /// Gets the codes disabled by this layer.
        /// </summary>
        public IReadOnlyCollection<string> DisabledCodes => _disabledCodes;

        /// <summary>
        /// Adds a sync rule created from the evaluation context on every <c>Resolve</c>.
        /// </summary>
        /// <param name="factory">Creates the rule; receives the context handed to <c>Resolve</c> (never null). Must not return null.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the builder is frozen.</exception>
        public RuleSetBuilder Add(Func<BusinessRuleContext, IBusinessRule> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            RequireNotFrozen();
            _factories.Add(factory);
            return this;
        }

        /// <summary>
        /// Adds a sync rule instance that every <c>Resolve</c> returns as-is. Suitable for stateless rules only.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the builder is frozen.</exception>
        public RuleSetBuilder Add(IBusinessRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return Add(_ => rule);
        }

        /// <summary>
        /// Adds an async rule created from the evaluation context on every <c>ResolveAsyncRules</c>.
        /// </summary>
        /// <param name="factory">Creates the rule; receives the context handed to <c>ResolveAsyncRules</c> (never null). Must not return null.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the builder is frozen.</exception>
        public RuleSetBuilder AddAsyncRule(Func<BusinessRuleContext, IAsyncBusinessRule> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            RequireNotFrozen();
            _asyncFactories.Add(factory);
            return this;
        }

        /// <summary>
        /// Adds an async rule instance that every <c>ResolveAsyncRules</c> returns as-is. Suitable for stateless rules only.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the builder is frozen.</exception>
        public RuleSetBuilder AddAsyncRule(IAsyncBusinessRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return AddAsyncRule(_ => rule);
        }

        /// <summary>
        /// Removes every rule (sync and async) whose <c>Code</c> equals <paramref name="code"/> from the layers resolved
        /// before this one: the less specific layers, and the equally specific layers declared earlier. Rules added by
        /// this layer or by later layers are not affected. Comparison is ordinal.
        /// </summary>
        /// <param name="code">The rule code to disable.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is empty or white space.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the builder is frozen.</exception>
        public RuleSetBuilder Disable(string code)
        {
            RequireCode(code);
            RequireNotFrozen();
            _disabledCodes.Add(code);
            return this;
        }

        /// <summary>
        /// Disables <paramref name="code"/> in the earlier layers and adds <paramref name="factory"/> to this one, so the
        /// new rule stands in for the old. The new rule is appended to the layer; it does not take the old rule's position.
        /// </summary>
        /// <param name="code">The code of the rule being replaced.</param>
        /// <param name="factory">Creates the replacement.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> or <paramref name="factory"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is empty or white space.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the builder is frozen.</exception>
        public RuleSetBuilder Replace(string code, Func<BusinessRuleContext, IBusinessRule> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            return Disable(code).Add(factory);
        }

        /// <summary>
        /// Disables <paramref name="code"/> in the earlier layers and adds the async <paramref name="factory"/> to this one.
        /// </summary>
        /// <param name="code">The code of the rule being replaced.</param>
        /// <param name="factory">Creates the replacement.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> or <paramref name="factory"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is empty or white space.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the builder is frozen.</exception>
        public RuleSetBuilder ReplaceAsyncRule(string code, Func<BusinessRuleContext, IAsyncBusinessRule> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            return Disable(code).AddAsyncRule(factory);
        }

        internal RuleSetLayer Freeze(int order)
        {
            _frozen = true;
            return new RuleSetLayer(
                Name,
                Scope,
                order,
                new ReadOnlyCollection<Func<BusinessRuleContext, IBusinessRule>>(_factories.ToArray()),
                new ReadOnlyCollection<Func<BusinessRuleContext, IAsyncBusinessRule>>(_asyncFactories.ToArray()),
                new HashSet<string>(_disabledCodes, StringComparer.Ordinal));
        }

        private void RequireNotFrozen()
        {
            if (_frozen)
            {
                throw new InvalidOperationException(FrozenMessage);
            }
        }

        private static void RequireCode(string code)
        {
            if (code == null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException(EmptyCodeMessage, nameof(code));
            }
        }
    }
}
