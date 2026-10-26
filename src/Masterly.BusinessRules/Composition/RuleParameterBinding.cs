using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The immutable snapshot a built rule keeps of its parameter declarations and injected providers, and the one
    /// place the resolution order is decided: the scope comes from <see cref="RuleParameters.ScopeKey"/> (null when
    /// absent), the sync provider from <see cref="RuleParameters.ContextKey"/> then the injected one, and the async
    /// provider from <see cref="RuleParameters.AsyncContextKey"/>, then <see cref="RuleParameters.ContextKey"/>, then the
    /// injected async provider, then the injected sync provider. Without any provider every parameter resolves to its
    /// fallback, so a parameterless evaluation can never throw for a missing provider.
    /// </summary>
    /// <remarks>
    /// A reserved key that is present but holds a value of another type (for example a tenant name stored as a string
    /// under <see cref="RuleParameters.ScopeKeyName"/>) is a configuration error, not an absent key: it throws
    /// <see cref="ContextValueTypeMismatchException"/> (wrapped by the checkers into <see cref="BusinessRuleContextException"/>)
    /// instead of silently resolving the global scope or ignoring the override. A stored null is treated as absent.
    /// </remarks>
    internal sealed class RuleParameterBinding
    {
        private static readonly RuleParameterBinding EmptyBinding = new RuleParameterBinding(Array.Empty<RuleParameterDeclaration>(), null, null);

        private readonly RuleParameterDeclaration[] _declarations;
        private readonly IRuleParameterProvider? _provider;
        private readonly IAsyncRuleParameterProvider? _asyncProvider;

        /// <summary>
        /// Initializes the binding from the declarations and the injected providers (either may be null).
        /// </summary>
        public RuleParameterBinding(RuleParameterDeclaration[] declarations, IRuleParameterProvider? provider, IAsyncRuleParameterProvider? asyncProvider)
        {
            _declarations = declarations;
            _provider = provider;
            _asyncProvider = asyncProvider;
        }

        /// <summary>
        /// Gets the binding of a rule that declared no parameters and injected no provider.
        /// </summary>
        public static RuleParameterBinding Empty => EmptyBinding;

        /// <summary>
        /// Resolves the parameters for a synchronous evaluation. A null context (the parameterless path) resolves the
        /// global scope through the injected provider.
        /// </summary>
        /// <exception cref="ContextValueTypeMismatchException">Thrown when a reserved key holds a value of another type.</exception>
        public RuleParameters Resolve(BusinessRuleContext? context)
        {
            RuleScope? scope = ResolveScope(context);
            if (_declarations.Length == 0 && scope == null)
            {
                return RuleParameters.Empty;
            }

            IRuleParameterProvider? provider = ResolveSyncProvider(context);
            return ResolveThrough(provider, scope);
        }

        /// <summary>
        /// Resolves the parameters for an asynchronous evaluation, preferring an async provider over a sync one. A null
        /// context resolves the global scope through the injected providers, mirroring <see cref="Resolve"/>.
        /// </summary>
        /// <exception cref="ContextValueTypeMismatchException">Thrown when a reserved key holds a value of another type.</exception>
        public async Task<RuleParameters> ResolveAsync(BusinessRuleContext? context, CancellationToken cancellationToken)
        {
            RuleScope? scope = ResolveScope(context);
            if (_declarations.Length == 0 && scope == null)
            {
                return RuleParameters.Empty;
            }

            if (TryReadReserved(context, RuleParameters.AsyncContextKey, out IAsyncRuleParameterProvider asyncFromContext))
            {
                return await ResolveThroughAsync(asyncFromContext, scope, cancellationToken);
            }

            if (TryReadReserved(context, RuleParameters.ContextKey, out IRuleParameterProvider fromContext))
            {
                return ResolveThrough(fromContext, scope);
            }

            if (_asyncProvider != null)
            {
                return await ResolveThroughAsync(_asyncProvider, scope, cancellationToken);
            }

            return ResolveThrough(_provider, scope);
        }

        private RuleParameters ResolveThrough(IRuleParameterProvider? provider, RuleScope? scope)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>(_declarations.Length, StringComparer.Ordinal);
            foreach (RuleParameterDeclaration declaration in _declarations)
            {
                values[declaration.Name] = declaration.Resolve(provider, scope);
            }

            return new RuleParameters(scope, values);
        }

        private async Task<RuleParameters> ResolveThroughAsync(IAsyncRuleParameterProvider provider, RuleScope? scope, CancellationToken cancellationToken)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>(_declarations.Length, StringComparer.Ordinal);
            foreach (RuleParameterDeclaration declaration in _declarations)
            {
                values[declaration.Name] = await declaration.ResolveAsync(provider, scope, cancellationToken);
            }

            return new RuleParameters(scope, values);
        }

        private IRuleParameterProvider? ResolveSyncProvider(BusinessRuleContext? context)
        {
            return TryReadReserved(context, RuleParameters.ContextKey, out IRuleParameterProvider fromContext) ? fromContext : _provider;
        }

        private static RuleScope? ResolveScope(BusinessRuleContext? context)
        {
            return TryReadReserved(context, RuleParameters.ScopeKey, out RuleScope scope) ? scope : null;
        }

        /// <summary>
        /// Reads a reserved key: false when the context is null, the key is absent or holds null; true with the value
        /// when it holds a <typeparamref name="T"/>. A present key holding another type throws through
        /// <see cref="BusinessRuleContext.Get{T}(ContextKey{T})"/>, so a misconfigured context never degrades silently.
        /// </summary>
        private static bool TryReadReserved<T>(BusinessRuleContext? context, ContextKey<T> key, out T value) where T : class
        {
            if (context == null || !context.ContainsKey(key))
            {
                value = null!; // the contract of the Try pattern: the out value is unspecified when the method returns false
                return false;
            }

            value = context.Get(key);
            return value != null;
        }
    }
}
