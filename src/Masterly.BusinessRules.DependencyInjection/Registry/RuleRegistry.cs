using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.DependencyInjection
{
    /// <summary>
    /// The immutable <see cref="IRuleRegistry"/> produced by <see cref="RuleRegistryBuilder.Build"/>. It holds rule
    /// factories, not instances, and is safe to share: nothing mutates after construction and every resolution creates
    /// fresh rules from the caller's context. See <see cref="IRuleRegistry"/> for the merge semantics.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleRegistry registry = RuleRegistry.Create()
    ///     .AddSet("checkout", set => set
    ///         .Add(ctx => new CartMustHaveItemsRule(ctx.Get(OrderKeys.Order)))
    ///         .Add(ctx => new OrderLimitRule(ctx.Get(OrderKeys.Order), parameters)))
    ///     .AddSet("checkout", RuleScope.Tenant("acme"), set => set
    ///         .Disable("CART.EMPTY")
    ///         .Add(ctx => new AcmePurchaseOrderRequiredRule()))
    ///     .Build();
    ///
    /// registry.CheckAll("checkout", RuleScope.Tenant("acme").With("region", "EU"), context);
    /// </code>
    /// </example>
    public sealed class RuleRegistry : IRuleRegistry
    {
        private readonly Dictionary<string, RuleSetLayer[]> _sets;
        private readonly ReadOnlyCollection<string> _setNames;

        internal RuleRegistry(IEnumerable<RuleSetLayer> layers)
        {
            _sets = layers
                .GroupBy(layer => layer.Name, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(layer => layer.Scope.Specificity).ThenBy(layer => layer.Order).ToArray(),
                    StringComparer.Ordinal);
            _setNames = new ReadOnlyCollection<string>(_sets.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray());
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> SetNames => _setNames;

        /// <summary>
        /// Starts a new registry.
        /// </summary>
        /// <returns>An empty builder.</returns>
        public static RuleRegistryBuilder Create()
        {
            return new RuleRegistryBuilder();
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        public bool Contains(string setName)
        {
            if (setName == null)
            {
                throw new ArgumentNullException(nameof(setName));
            }

            return _sets.ContainsKey(setName);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a factory returns null.</exception>
        public IReadOnlyList<IBusinessRule> Resolve(string setName, RuleScope? scope = null, BusinessRuleContext? context = null)
        {
            return ResolveRules(setName, scope, PrepareContext(scope, context), layer => layer.Factories);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a factory returns null.</exception>
        public IReadOnlyList<IAsyncBusinessRule> ResolveAsyncRules(string setName, RuleScope? scope = null, BusinessRuleContext? context = null)
        {
            return ResolveRules(setName, scope, PrepareContext(scope, context), layer => layer.AsyncFactories);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        public void CheckAll(string setName, RuleScope? scope = null, BusinessRuleContext? context = null)
        {
            CheckAll(setName, scope, context, new RuleEvaluationOptions());
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="options"/> is null.</exception>
        public void CheckAll(string setName, RuleScope? scope, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            RequireOptions(options);
            BusinessRuleContext prepared = PrepareContext(scope, context);
            BusinessRuleChecker.CheckAll(ResolveRules(setName, scope, prepared, layer => layer.Factories), prepared, options);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        public RuleEvaluationResult EvaluateAll(string setName, RuleScope? scope = null, BusinessRuleContext? context = null)
        {
            return EvaluateAll(setName, scope, context, new RuleEvaluationOptions());
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="options"/> is null.</exception>
        public RuleEvaluationResult EvaluateAll(string setName, RuleScope? scope, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            RequireOptions(options);
            BusinessRuleContext prepared = PrepareContext(scope, context);
            return BusinessRuleChecker.EvaluateAll(ResolveRules(setName, scope, prepared, layer => layer.Factories), prepared, options);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        public Task CheckAllAsync(string setName, RuleScope? scope = null, BusinessRuleContext? context = null, CancellationToken cancellationToken = default)
        {
            return CheckAllAsync(setName, scope, context, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="options"/> is null.</exception>
        public Task CheckAllAsync(string setName, RuleScope? scope, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            RequireOptions(options);
            BusinessRuleContext prepared = PrepareContext(scope, context);
            IReadOnlyList<IAsyncBusinessRule> rules = ResolveRules(setName, scope, prepared, layer => layer.AsyncFactories);
            return AsyncBusinessRuleChecker.CheckAllAsync(prepared, rules, options, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(string setName, RuleScope? scope = null, BusinessRuleContext? context = null, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsync(setName, scope, context, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="options"/> is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(string setName, RuleScope? scope, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            RequireOptions(options);
            BusinessRuleContext prepared = PrepareContext(scope, context);
            IReadOnlyList<IAsyncBusinessRule> rules = ResolveRules(setName, scope, prepared, layer => layer.AsyncFactories);
            return AsyncBusinessRuleChecker.EvaluateAllAsync(prepared, rules, options, cancellationToken);
        }

        private IReadOnlyList<TRule> ResolveRules<TRule>(
            string setName,
            RuleScope? scope,
            BusinessRuleContext context,
            Func<RuleSetLayer, IReadOnlyList<Func<BusinessRuleContext, TRule>>> factoriesOf)
            where TRule : class, IBusinessRuleMetadata
        {
            RuleSetLayer[] layers = LayersOf(setName);
            RuleScope requested = scope ?? RuleScope.Global;
            List<TRule> rules = new List<TRule>();

            foreach (RuleSetLayer layer in layers)
            {
                if (!layer.Scope.AppliesTo(requested))
                {
                    continue;
                }

                if (layer.DisabledCodes.Count > 0)
                {
                    rules.RemoveAll(rule => layer.DisabledCodes.Contains(rule.Code));
                }

                IReadOnlyList<Func<BusinessRuleContext, TRule>> factories = factoriesOf(layer);
                for (int position = 0; position < factories.Count; position++)
                {
                    TRule? rule = factories[position](context);
                    if (rule == null)
                    {
                        throw new InvalidOperationException(
                            $"The rule factory at position {position} of rule set '{layer.Name}' (scope {layer.Scope}) returned null.");
                    }

                    rules.Add(rule);
                }
            }

            return new ReadOnlyCollection<TRule>(rules);
        }

        private RuleSetLayer[] LayersOf(string setName)
        {
            if (setName == null)
            {
                throw new ArgumentNullException(nameof(setName));
            }

            if (!_sets.TryGetValue(setName, out RuleSetLayer[] layers))
            {
                throw new RuleSetNotFoundException(setName, _setNames);
            }

            return layers;
        }

        private static BusinessRuleContext PrepareContext(RuleScope? scope, BusinessRuleContext? context)
        {
            BusinessRuleContext prepared = context ?? new BusinessRuleContext();
            if (!prepared.ContainsKey(RuleParameters.ScopeKey))
            {
                prepared.Set(RuleParameters.ScopeKey, scope ?? RuleScope.Global);
            }

            return prepared;
        }

        private static void RequireOptions(object options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
        }
    }
}
