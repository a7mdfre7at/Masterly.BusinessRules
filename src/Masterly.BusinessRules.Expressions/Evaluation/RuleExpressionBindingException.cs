using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Thrown during evaluation when an identifier cannot be bound: a root identifier or <c>@parameter</c> with no
    /// matching context entry (or parameter value), or a member that does not exist on, or is not readable from, the
    /// value it is applied to. <see cref="Identifier"/> is the unbound path (<c>amount</c>, <c>@limit</c>,
    /// <c>customer.Tier</c>) and <see cref="AvailableKeys"/> lists what could have been bound instead, sorted ordinally.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleExpressionBindingException error = Assert.Throws&lt;RuleExpressionBindingException&gt;(() => expression.Evaluate(context));
    /// // error.Identifier == "amount", error.AvailableKeys == ["customer", "orderId"]
    /// </code>
    /// </example>
    public sealed class RuleExpressionBindingException : Exception
    {
        /// <summary>
        /// Creates the exception.
        /// </summary>
        /// <param name="identifier">The identifier, parameter (with its <c>@</c>) or member path that could not be bound.</param>
        /// <param name="availableKeys">The keys or members that were available instead.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> or <paramref name="availableKeys"/> is null.</exception>
        public RuleExpressionBindingException(string identifier, IEnumerable<string> availableKeys)
            : base(FormatMessage(identifier, availableKeys, out IReadOnlyList<string> snapshot))
        {
            Identifier = identifier;
            AvailableKeys = snapshot;
        }

        /// <summary>
        /// Gets the identifier, parameter or member path that could not be bound.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Gets the keys (for a root identifier or parameter) or readable members (for a member access) that were
        /// available, sorted ordinally.
        /// </summary>
        public IReadOnlyList<string> AvailableKeys { get; }

        private static string FormatMessage(string identifier, IEnumerable<string> availableKeys, out IReadOnlyList<string> snapshot)
        {
            if (identifier == null)
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            if (availableKeys == null)
            {
                throw new ArgumentNullException(nameof(availableKeys));
            }

            snapshot = availableKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            if (snapshot.Count == 0)
            {
                return $"'{identifier}' could not be bound; nothing is available to bind it to.";
            }

            return $"'{identifier}' could not be bound; available: {string.Join(", ", snapshot)}.";
        }
    }
}
