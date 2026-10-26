using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The parameter values resolved for one evaluation of a built rule: every parameter the builder declared with
    /// <c>WithParameter&lt;T&gt;(name, fallback)</c>, holding the provider's value for the evaluation's <see cref="Scope"/>
    /// or the declared fallback. Handed to the <c>(ctx, p) =&gt; ...</c> condition overloads of the four builders.
    /// </summary>
    /// <remarks>
    /// <para>Also hosts the reserved context keys of the parameter machinery. A provider stored under <see cref="ContextKey"/>
    /// (sync) or <see cref="AsyncContextKey"/> (async) overrides the provider injected with <c>WithParameters(...)</c> for
    /// that evaluation only; it is an optional override, not the primary wiring, so the context never becomes a service
    /// locator. The <see cref="RuleScope"/> of the evaluation is read from <see cref="ScopeKey"/>; when absent the global
    /// scope is requested.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// IBusinessRule rule = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    ///     .WithParameters(parameters)
    ///     .WithParameter&lt;decimal&gt;("maxOrderAmount", fallback: 10_000m)
    ///     .When((ctx, p) => ctx.Get&lt;decimal&gt;("amount") &gt; p.Get&lt;decimal&gt;("maxOrderAmount"))
    ///     .Build();
    ///
    /// context.Set(RuleParameters.ScopeKey, RuleScope.Tenant("acme"));
    /// </code>
    /// </example>
    public sealed class RuleParameters
    {
        /// <summary>
        /// The name of the context entry holding the sync provider override.
        /// </summary>
        public const string ProviderKeyName = "__ruleParameterProvider";

        /// <summary>
        /// The name of the context entry holding the async provider override.
        /// </summary>
        public const string AsyncProviderKeyName = "__asyncRuleParameterProvider";

        /// <summary>
        /// The name of the context entry holding the <see cref="RuleScope"/> of the evaluation.
        /// </summary>
        public const string ScopeKeyName = "__ruleScope";

        /// <summary>
        /// The typed context key under which an <see cref="IRuleParameterProvider"/> overrides the provider injected
        /// into a built rule for that evaluation (sync rules, and async rules when <see cref="AsyncContextKey"/> is absent).
        /// </summary>
        public static readonly ContextKey<IRuleParameterProvider> ContextKey = new ContextKey<IRuleParameterProvider>(ProviderKeyName);

        /// <summary>
        /// The typed context key under which an <see cref="IAsyncRuleParameterProvider"/> overrides the providers
        /// injected into a built async rule for that evaluation.
        /// </summary>
        public static readonly ContextKey<IAsyncRuleParameterProvider> AsyncContextKey = new ContextKey<IAsyncRuleParameterProvider>(AsyncProviderKeyName);

        /// <summary>
        /// The typed context key under which the <see cref="RuleScope"/> of the evaluation (the tenant, the region) is
        /// stored. Absent means the global scope.
        /// </summary>
        public static readonly ContextKey<RuleScope> ScopeKey = new ContextKey<RuleScope>(ScopeKeyName);

        private static readonly RuleParameters EmptyParameters = new RuleParameters(null, new Dictionary<string, object?>(StringComparer.Ordinal));

        private readonly Dictionary<string, object?> _values;

        internal RuleParameters(RuleScope? scope, Dictionary<string, object?> values)
        {
            Scope = scope;
            _values = values;
        }

        /// <summary>
        /// Gets the parameters of a rule that declared none, resolved for the global scope.
        /// </summary>
        public static RuleParameters Empty => EmptyParameters;

        /// <summary>
        /// Gets the scope the values were resolved for, or null for the global scope.
        /// </summary>
        public RuleScope? Scope { get; }

        /// <summary>
        /// Gets the number of declared parameters.
        /// </summary>
        public int Count => _values.Count;

        /// <summary>
        /// Gets the names of the declared parameters.
        /// </summary>
        public IReadOnlyCollection<string> Names => _values.Keys;

        /// <summary>
        /// Returns whether <paramref name="name"/> was declared.
        /// </summary>
        /// <param name="name">The parameter name. Compared ordinally.</param>
        /// <returns>True when the parameter was declared.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public bool Contains(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _values.ContainsKey(name);
        }

        /// <summary>
        /// Gets the resolved value of a declared parameter.
        /// </summary>
        /// <typeparam name="T">The type the parameter was declared with.</typeparam>
        /// <param name="name">The parameter name. Compared ordinally.</param>
        /// <returns>The provider's value for the scope, or the declared fallback.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the parameter was not declared; the message lists the declared names.</exception>
        /// <exception cref="InvalidCastException">Thrown when the resolved value is not a <typeparamref name="T"/>.</exception>
        public T Get<T>(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!_values.TryGetValue(name, out object? stored))
            {
                throw new KeyNotFoundException(MissingParameterMessage(name));
            }

            if (stored is T typed)
            {
                return typed;
            }

            if (stored == null && default(T) == null)
            {
                // T allows null and the resolved value is null: null is the faithful answer.
                return default!;
            }

            throw new InvalidCastException($"Parameter '{name}' holds {stored?.GetType().Name ?? "null"}, requested {typeof(T).Name}.");
        }

        /// <summary>
        /// Tries to get the resolved value of a declared parameter. Returns false when the parameter was not declared or
        /// its value is not a <typeparamref name="T"/>; never throws for either.
        /// </summary>
        /// <typeparam name="T">The type the parameter was declared with.</typeparam>
        /// <param name="name">The parameter name. Compared ordinally.</param>
        /// <param name="value">The resolved value, or <c>default</c> when the method returns false.</param>
        /// <returns>True when a value of type <typeparamref name="T"/> is held under the name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public bool TryGet<T>(string name, out T value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (_values.TryGetValue(name, out object? stored) && stored is T typed)
            {
                value = typed;
                return true;
            }

            value = default!; // the contract of the Try pattern: the out value is unspecified when the method returns false
            return false;
        }

        private string MissingParameterMessage(string name)
        {
            if (_values.Count == 0)
            {
                return $"Parameter '{name}' was not declared. The rule declares no parameters; add WithParameter<T>(\"{name}\", fallback) to the builder.";
            }

            string declared = string.Join(", ", _values.Keys.OrderBy(key => key, StringComparer.Ordinal));
            return $"Parameter '{name}' was not declared. Declared parameters: {declared}";
        }
    }
}
