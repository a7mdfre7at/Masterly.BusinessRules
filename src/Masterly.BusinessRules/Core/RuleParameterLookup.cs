using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The outcome of an asynchronous parameter lookup (<see cref="IAsyncRuleParameterProvider.TryGetAsync{T}"/>):
    /// either <see cref="Found"/> with a <see cref="Value"/>, or <see cref="Missing"/>. It plays the role of the
    /// <c>bool</c> + <c>out</c> pair of the synchronous <see cref="IRuleParameterProvider.TryGet{T}"/>. The default
    /// instance is <see cref="Missing"/>.
    /// </summary>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <example>
    /// <code>
    /// RuleParameterLookup&lt;decimal&gt; found = RuleParameterLookup&lt;decimal&gt;.Of(50_000m);
    /// RuleParameterLookup&lt;decimal&gt; missing = RuleParameterLookup&lt;decimal&gt;.Missing;
    /// decimal limit = missing.GetValueOrFallback(10_000m);   // 10000
    /// </code>
    /// </example>
    public readonly struct RuleParameterLookup<T> : IEquatable<RuleParameterLookup<T>>
    {
        private const string FoundText = "found";
        private const string MissingText = "missing";
        private const int HashMultiplier = 397;

        private RuleParameterLookup(bool found, T value)
        {
            Found = found;
            Value = value;
        }

        /// <summary>
        /// Gets the lookup that found nothing. Equal to <c>default(RuleParameterLookup&lt;T&gt;)</c>.
        /// </summary>
        public static RuleParameterLookup<T> Missing => default;

        /// <summary>
        /// Gets whether a value was found.
        /// </summary>
        public bool Found { get; }

        /// <summary>
        /// Gets the value found, or <c>default</c> when <see cref="Found"/> is false.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Creates a lookup that found <paramref name="value"/>. A null value is a found null, not a missing value.
        /// </summary>
        /// <param name="value">The value found.</param>
        /// <returns>The lookup.</returns>
        public static RuleParameterLookup<T> Of(T value)
        {
            return new RuleParameterLookup<T>(true, value);
        }

        /// <summary>
        /// Returns <see cref="Value"/> when found, otherwise <paramref name="fallback"/>.
        /// </summary>
        /// <param name="fallback">The value to return when nothing was found.</param>
        /// <returns>The found value or the fallback.</returns>
        public T GetValueOrFallback(T fallback)
        {
            return Found ? Value : fallback;
        }

        /// <inheritdoc />
        public bool Equals(RuleParameterLookup<T> other)
        {
            return Found == other.Found && EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is RuleParameterLookup<T> other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Found ? 1 : 0) * HashMultiplier ^ (Value == null ? 0 : EqualityComparer<T>.Default.GetHashCode(Value));
            }
        }

        /// <summary>
        /// Returns <c>found: value</c> or <c>missing</c>.
        /// </summary>
        /// <returns>The textual form of the lookup.</returns>
        public override string ToString()
        {
            return Found ? $"{FoundText}: {Value}" : MissingText;
        }

        /// <summary>
        /// Compares two lookups for equality.
        /// </summary>
        /// <param name="left">The first lookup.</param>
        /// <param name="right">The second lookup.</param>
        /// <returns>True when both are missing, or both are found with equal values.</returns>
        public static bool operator ==(RuleParameterLookup<T> left, RuleParameterLookup<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two lookups for inequality.
        /// </summary>
        /// <param name="left">The first lookup.</param>
        /// <param name="right">The second lookup.</param>
        /// <returns>True when exactly one is found or the values differ.</returns>
        public static bool operator !=(RuleParameterLookup<T> left, RuleParameterLookup<T> right)
        {
            return !left.Equals(right);
        }
    }
}
