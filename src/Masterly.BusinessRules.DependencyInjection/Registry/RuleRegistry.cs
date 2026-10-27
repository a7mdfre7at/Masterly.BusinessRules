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
        private const string CodeSeparator = ", ";

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
            RuleSetLayer[] layers = LayersOf(setName);
            RuleScope requested = scope ?? RuleScope.Global;
            return ResolveRules(layers, requested, StampScope(context, requested), layer => layer.Factories);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a factory returns null.</exception>
        public IReadOnlyList<IAsyncBusinessRule> ResolveAsyncRules(string setName, RuleScope? scope = null, BusinessRuleContext? context = null)
        {
            RuleSetLayer[] layers = LayersOf(setName);
            RuleScope requested = scope ?? RuleScope.Global;
            return ResolveRules(layers, requested, StampScope(context, requested), layer => layer.AsyncFactories);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the resolved set has async rules, or a factory returns null.</exception>
        public void CheckAll(string setName, RuleScope? scope = null, BusinessRuleContext? context = null)
        {
            CheckAll(setName, scope, context, new RuleEvaluationOptions());
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the resolved set has async rules, or a factory returns null.</exception>
        public void CheckAll(string setName, RuleScope? scope, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            RequireOptions(options);
            RuleSetLayer[] layers = LayersOf(setName);
            RuleScope requested = scope ?? RuleScope.Global;
            BusinessRuleContext prepared = context ?? new BusinessRuleContext();
            RuleScopeOverride scopeOverride = RuleScopeOverride.Apply(prepared, requested);
            try
            {
                BusinessRuleChecker.CheckAll(ResolveSynchronousOnly(setName, layers, requested, prepared), prepared, options);
            }
            finally
            {
                scopeOverride.Restore();
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the resolved set has async rules, or a factory returns null.</exception>
        public RuleEvaluationResult EvaluateAll(string setName, RuleScope? scope = null, BusinessRuleContext? context = null)
        {
            return EvaluateAll(setName, scope, context, new RuleEvaluationOptions());
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the resolved set has async rules, or a factory returns null.</exception>
        public RuleEvaluationResult EvaluateAll(string setName, RuleScope? scope, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            RequireOptions(options);
            RuleSetLayer[] layers = LayersOf(setName);
            RuleScope requested = scope ?? RuleScope.Global;
            BusinessRuleContext prepared = context ?? new BusinessRuleContext();
            RuleScopeOverride scopeOverride = RuleScopeOverride.Apply(prepared, requested);
            try
            {
                return BusinessRuleChecker.EvaluateAll(ResolveSynchronousOnly(setName, layers, requested, prepared), prepared, options);
            }
            finally
            {
                scopeOverride.Restore();
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        public Task CheckAllAsync(string setName, RuleScope? scope = null, BusinessRuleContext? context = null, CancellationToken cancellationToken = default)
        {
            return CheckAllAsync(setName, scope, context, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="options"/> is null.</exception>
        public async Task CheckAllAsync(string setName, RuleScope? scope, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            RequireOptions(options);
            RuleSetLayer[] layers = LayersOf(setName);
            RuleScope requested = scope ?? RuleScope.Global;
            BusinessRuleContext prepared = context ?? new BusinessRuleContext();
            RuleScopeOverride scopeOverride = RuleScopeOverride.Apply(prepared, requested);
            try
            {
                await AsyncBusinessRuleChecker.CheckAllAsync(prepared, ResolveAll(layers, requested, prepared), options, cancellationToken);
            }
            finally
            {
                scopeOverride.Restore();
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(string setName, RuleScope? scope = null, BusinessRuleContext? context = null, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsync(setName, scope, context, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setName"/> or <paramref name="options"/> is null.</exception>
        public async Task<AsyncRuleEvaluationResult> EvaluateAllAsync(string setName, RuleScope? scope, BusinessRuleContext? context, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            RequireOptions(options);
            RuleSetLayer[] layers = LayersOf(setName);
            RuleScope requested = scope ?? RuleScope.Global;
            BusinessRuleContext prepared = context ?? new BusinessRuleContext();
            RuleScopeOverride scopeOverride = RuleScopeOverride.Apply(prepared, requested);
            try
            {
                return await AsyncBusinessRuleChecker.EvaluateAllAsync(prepared, ResolveAll(layers, requested, prepared), options, cancellationToken);
            }
            finally
            {
                scopeOverride.Restore();
            }
        }

        /// <summary>
        /// Stores the requested scope under <see cref="RuleParameters.ScopeKey"/> (replacing any value) so the resolved
        /// rules, evaluated later with this context, resolve their parameters for the scope their layers were chosen for.
        /// </summary>
        private static BusinessRuleContext StampScope(BusinessRuleContext? context, RuleScope requested)
        {
            BusinessRuleContext prepared = context ?? new BusinessRuleContext();
            prepared.Set(RuleParameters.ScopeKey, requested);
            return prepared;
        }

        /// <summary>
        /// Resolves the sync rules of a set for a synchronous batch, refusing a set that also resolves async rules: a
        /// synchronous batch cannot evaluate them without blocking, and silently dropping them would pass a broken set.
        /// </summary>
        private static IReadOnlyList<IBusinessRule> ResolveSynchronousOnly(string setName, RuleSetLayer[] layers, RuleScope requested, BusinessRuleContext context)
        {
            IReadOnlyList<IAsyncBusinessRule> asyncRules = ResolveRules(layers, requested, context, layer => layer.AsyncFactories);
            if (asyncRules.Count > 0)
            {
                string codes = string.Join(CodeSeparator, asyncRules.Select(rule => rule.Code));
                throw new InvalidOperationException(
                    $"Rule set '{setName}' (scope {requested}) has {asyncRules.Count} asynchronous rule(s) ({codes}) that a synchronous batch cannot evaluate without blocking. Call CheckAllAsync/EvaluateAllAsync, or evaluate Resolve(...) through BusinessRuleChecker directly.");
            }

            return ResolveRules(layers, requested, context, layer => layer.Factories);
        }

        /// <summary>
        /// Resolves the whole set for an asynchronous batch: the sync rules adapted with <c>ToAsync()</c>, then the async rules.
        /// </summary>
        private static IReadOnlyList<IAsyncBusinessRule> ResolveAll(RuleSetLayer[] layers, RuleScope requested, BusinessRuleContext context)
        {
            IReadOnlyList<IBusinessRule> syncRules = ResolveRules(layers, requested, context, layer => layer.Factories);
            IReadOnlyList<IAsyncBusinessRule> asyncRules = ResolveRules(layers, requested, context, layer => layer.AsyncFactories);
            List<IAsyncBusinessRule> rules = new List<IAsyncBusinessRule>(syncRules.Count + asyncRules.Count);
            rules.AddRange(syncRules.Select(rule => rule.ToAsync()));
            rules.AddRange(asyncRules);
            return rules;
        }

        private static IReadOnlyList<TRule> ResolveRules<TRule>(
            RuleSetLayer[] layers,
            RuleScope requested,
            BusinessRuleContext context,
            Func<RuleSetLayer, IReadOnlyList<Func<BusinessRuleContext, TRule>>> factoriesOf)
            where TRule : class, IBusinessRuleMetadata
        {
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

        private static void RequireOptions(object options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
        }
    }
}
