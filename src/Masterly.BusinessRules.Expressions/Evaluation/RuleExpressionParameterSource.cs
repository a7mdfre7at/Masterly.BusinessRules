using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Resolves <c>@name</c> parameter nodes for one evaluation. The lookup order mirrors the core builders: the
    /// rule's declared <see cref="RuleParameters"/> first, then a provider stored on the context under
    /// <see cref="RuleParameters.AsyncContextKey"/> (async only) or <see cref="RuleParameters.ContextKey"/>, then the
    /// injected async provider (async only), then the injected sync provider. The context bag itself is the
    /// interpreter's final fallback. The scope comes from <see cref="RuleParameters.ScopeKey"/>; absent means global.
    /// </summary>
    internal sealed class RuleExpressionParameterSource
    {
        private static readonly RuleExpressionParameterSource EmptySource = new RuleExpressionParameterSource(null, null, null, null, null);

        private readonly RuleParameters? _declared;
        private readonly IRuleParameterProvider? _contextProvider;
        private readonly IRuleParameterProvider? _injected;
        private readonly RuleScope? _scope;
        private readonly IReadOnlyDictionary<string, object?>? _resolved;

        private RuleExpressionParameterSource(
            RuleParameters? declared,
            IRuleParameterProvider? contextProvider,
            IRuleParameterProvider? injected,
            RuleScope? scope,
            IReadOnlyDictionary<string, object?>? resolved)
        {
            _declared = declared;
            _contextProvider = contextProvider;
            _injected = injected;
            _scope = scope;
            _resolved = resolved;
        }

        /// <summary>
        /// Gets a source that resolves nothing, so every parameter falls back to the context bag.
        /// </summary>
        public static RuleExpressionParameterSource Empty => EmptySource;

        /// <summary>
        /// Creates the source for a synchronous evaluation; providers are consulted lazily per parameter.
        /// </summary>
        public static RuleExpressionParameterSource ForSync(BusinessRuleContext context, RuleParameters? declared, IRuleParameterProvider? injected)
        {
            IRuleParameterProvider? contextProvider = ReadReserved(context, RuleParameters.ContextKey);
            if (declared == null && contextProvider == null && injected == null)
            {
                return EmptySource;
            }

            return new RuleExpressionParameterSource(declared, contextProvider, injected, ReadReserved(context, RuleParameters.ScopeKey), null);
        }

        /// <summary>
        /// Creates the source for an asynchronous evaluation by resolving every named parameter up front, so the
        /// interpreter itself stays synchronous.
        /// </summary>
        public static async Task<RuleExpressionParameterSource> ForAsync(
            BusinessRuleContext context,
            IReadOnlyList<string> names,
            RuleParameters? declared,
            IAsyncRuleParameterProvider? injectedAsync,
            IRuleParameterProvider? injectedSync,
            CancellationToken cancellationToken)
        {
            IAsyncRuleParameterProvider? asyncContextProvider = ReadReserved(context, RuleParameters.AsyncContextKey);
            IRuleParameterProvider? syncContextProvider = ReadReserved(context, RuleParameters.ContextKey);
            if (names.Count == 0 || (declared == null && asyncContextProvider == null && syncContextProvider == null && injectedAsync == null && injectedSync == null))
            {
                return EmptySource;
            }

            RuleScope? scope = ReadReserved(context, RuleParameters.ScopeKey);
            Dictionary<string, object?> resolved = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (declared != null && declared.Contains(name))
                {
                    resolved[name] = declared.Get<object>(name);
                    continue;
                }

                if (asyncContextProvider != null && await TryResolveAsync(asyncContextProvider, name, scope, resolved, cancellationToken))
                {
                    continue;
                }

                if (syncContextProvider != null && TryResolve(syncContextProvider, name, scope, resolved))
                {
                    continue;
                }

                if (injectedAsync != null && await TryResolveAsync(injectedAsync, name, scope, resolved, cancellationToken))
                {
                    continue;
                }

                if (injectedSync != null)
                {
                    TryResolve(injectedSync, name, scope, resolved);
                }
            }

            return new RuleExpressionParameterSource(null, null, null, scope, resolved);
        }

        /// <summary>
        /// Tries to resolve <paramref name="name"/>; false means the interpreter should consult the context bag.
        /// </summary>
        public bool TryResolve(string name, out object? value)
        {
            if (_resolved != null)
            {
                return _resolved.TryGetValue(name, out value);
            }

            if (_declared != null && _declared.Contains(name))
            {
                value = _declared.Get<object>(name);
                return true;
            }

            if (_contextProvider != null && _contextProvider.TryGet(name, _scope, out value))
            {
                return true;
            }

            if (_injected != null && _injected.TryGet(name, _scope, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private static bool TryResolve(IRuleParameterProvider provider, string name, RuleScope? scope, Dictionary<string, object?> resolved)
        {
            if (provider.TryGet(name, scope, out object? value))
            {
                resolved[name] = value;
                return true;
            }

            return false;
        }

        private static async Task<bool> TryResolveAsync(IAsyncRuleParameterProvider provider, string name, RuleScope? scope, Dictionary<string, object?> resolved, CancellationToken cancellationToken)
        {
            RuleParameterLookup<object?> lookup = await provider.TryGetAsync<object?>(name, scope, cancellationToken);
            if (lookup.Found)
            {
                resolved[name] = lookup.Value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads a reserved key: absent or stored null means none; present with another type throws
        /// <see cref="ContextValueTypeMismatchException"/>, exactly as the core builders do.
        /// </summary>
        private static T? ReadReserved<T>(BusinessRuleContext context, ContextKey<T> key) where T : class
            => context.ContainsKey(key) ? context.Get(key) : null;
    }
}
