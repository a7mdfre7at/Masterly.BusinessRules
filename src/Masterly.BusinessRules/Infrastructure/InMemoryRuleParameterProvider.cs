using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A thread-safe, in-memory <see cref="IRuleParameterProvider"/> and <see cref="IAsyncRuleParameterProvider"/> for
    /// tests, local development and small deployments. Values are set per <see cref="RuleScope"/> with
    /// <see cref="Set{T}(string, T, RuleScope)"/> (or globally with <see cref="Set{T}(string, T)"/>) and can be changed
    /// at any time. A lookup returns the value of the most specific scope that applies to the requested one
    /// (<see cref="RuleScope.AppliesTo"/>); among equally specific scopes the most recently set value wins; an unknown
    /// parameter, or one with no applicable scope, reports nothing so the rule's fallback is used. Names are compared ordinally.
    /// </summary>
    /// <example>
    /// <code>
    /// InMemoryRuleParameterProvider parameters = new InMemoryRuleParameterProvider()
    ///     .Set("maxOrderAmount", 10_000m)
    ///     .Set("maxOrderAmount", 50_000m, RuleScope.Tenant("acme"));
    ///
    /// parameters.TryGet("maxOrderAmount", RuleScope.Tenant("acme").With("region", "EU"), out decimal limit);   // 50000
    /// </code>
    /// </example>
    public sealed class InMemoryRuleParameterProvider : IRuleParameterProvider, IAsyncRuleParameterProvider
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, List<ScopedValue>> _values = new Dictionary<string, List<ScopedValue>>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the number of (name, scope) entries that have been set.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    int count = 0;
                    foreach (List<ScopedValue> entries in _values.Values)
                    {
                        count += entries.Count;
                    }

                    return count;
                }
            }
        }

        /// <summary>
        /// Sets or replaces the global value of a parameter.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The value.</param>
        /// <returns>This provider, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public InMemoryRuleParameterProvider Set<T>(string name, T value)
        {
            return Set(name, value, RuleScope.Global);
        }

        /// <summary>
        /// Sets or replaces the value of a parameter for a scope.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The value.</param>
        /// <param name="scope">The scope the value applies to; <see cref="RuleScope.Global"/> for everybody.</param>
        /// <returns>This provider, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="scope"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or white space.</exception>
        public InMemoryRuleParameterProvider Set<T>(string name, T value, RuleScope scope)
        {
            RequireName(name);
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            lock (_lock)
            {
                if (!_values.TryGetValue(name, out List<ScopedValue> entries))
                {
                    entries = new List<ScopedValue>();
                    _values[name] = entries;
                }

                entries.RemoveAll(entry => entry.Scope.Equals(scope));
                entries.Add(new ScopedValue(scope, value, typeof(T)));
            }

            return this;
        }

        /// <summary>
        /// Removes every value of a parameter, in every scope.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <returns>True when at least one value was removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public bool Remove(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            lock (_lock)
            {
                return _values.Remove(name);
            }
        }

        /// <summary>
        /// Removes the value of a parameter for one scope.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="scope">The scope whose value is removed.</param>
        /// <returns>True when a value was set for that scope and has been removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="scope"/> is null.</exception>
        public bool Remove(string name, RuleScope scope)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            lock (_lock)
            {
                if (!_values.TryGetValue(name, out List<ScopedValue> entries))
                {
                    return false;
                }

                int removed = entries.RemoveAll(entry => entry.Scope.Equals(scope));
                if (entries.Count == 0)
                {
                    _values.Remove(name);
                }

                return removed > 0;
            }
        }

        /// <inheritdoc />
        /// <remarks>Returns the value of the most specific applicable scope; a null <paramref name="scope"/> requests the global scope.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="InvalidCastException">Thrown when the selected value is not a <typeparamref name="T"/>; a value stored with the wrong type is a configuration error, not a missing value.</exception>
        public bool TryGet<T>(string name, RuleScope? scope, out T value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            RuleScope requested = scope ?? RuleScope.Global;
            ScopedValue? selected = null;

            lock (_lock)
            {
                if (_values.TryGetValue(name, out List<ScopedValue> entries))
                {
                    foreach (ScopedValue entry in entries)
                    {
                        if (entry.Scope.AppliesTo(requested) && (selected == null || entry.Scope.Specificity >= selected.Scope.Specificity))
                        {
                            selected = entry;
                        }
                    }
                }
            }

            if (selected == null)
            {
                value = default!; // the contract of the Try pattern: the out value is unspecified when the method returns false
                return false;
            }

            if (selected.Value is T typed)
            {
                value = typed;
                return true;
            }

            if (selected.Value == null && default(T) == null)
            {
                // T allows null and the stored value is null: null is the faithful answer.
                value = default!;
                return true;
            }

            throw new InvalidCastException($"Parameter '{name}' in scope {selected.Scope} holds {selected.ValueType.Name}, requested {typeof(T).Name}.");
        }

        /// <inheritdoc />
        /// <remarks>Completes synchronously with <see cref="TryGet{T}"/>; a cancelled token throws before the lookup.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="InvalidCastException">Thrown when the selected value is not a <typeparamref name="T"/>.</exception>
        public Task<RuleParameterLookup<T>> TryGetAsync<T>(string name, RuleScope? scope, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryGet(name, scope, out T value) ? RuleParameterLookup<T>.Of(value) : RuleParameterLookup<T>.Missing);
        }

        private static void RequireName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The parameter name cannot be empty or white space.", nameof(name));
            }
        }

        private sealed class ScopedValue
        {
            public ScopedValue(RuleScope scope, object? value, Type valueType)
            {
                Scope = scope;
                Value = value;
                ValueType = valueType;
            }

            public RuleScope Scope { get; }

            public object? Value { get; }

            public Type ValueType { get; }
        }
    }
}
