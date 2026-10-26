using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Extension methods for composing and transforming asynchronous business rules.
    /// </summary>
    public static class AsyncBusinessRuleExtensions
    {
        /// <summary>
        /// Creates a new async rule that is broken only when both rules are broken (logical AND over the broken state).
        /// The returned <see cref="AndAsyncBusinessRule"/> keeps both operands, forwards the context and cancellation
        /// token to them and reports the synthesised code <c>A+B</c>.
        /// </summary>
        /// <param name="first">The first async rule.</param>
        /// <param name="second">The second async rule.</param>
        /// <returns>A new async rule that is broken when both input rules are broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either rule is null.</exception>
        public static IAsyncBusinessRule And(this IAsyncBusinessRule first, IAsyncBusinessRule second)
        {
            return new AndAsyncBusinessRule(first, second);
        }

        /// <summary>
        /// Creates a new async rule that is broken when either rule is broken (logical OR over the broken state).
        /// The returned <see cref="OrAsyncBusinessRule"/> keeps both operands, forwards the context and cancellation
        /// token to them and reports the synthesised code <c>A|B</c>.
        /// </summary>
        /// <param name="first">The first async rule.</param>
        /// <param name="second">The second async rule.</param>
        /// <returns>A new async rule that is broken when either input rule is broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either rule is null.</exception>
        public static IAsyncBusinessRule Or(this IAsyncBusinessRule first, IAsyncBusinessRule second)
        {
            return new OrAsyncBusinessRule(first, second);
        }

        /// <summary>
        /// Creates a new async rule that inverts the result of the original rule (logical NOT).
        /// The returned <see cref="NotAsyncBusinessRule"/> keeps its operand and reports the synthesised code <c>!A</c>.
        /// </summary>
        /// <param name="rule">The async rule to invert.</param>
        /// <returns>A new async rule that is broken when the original passes, and passes when the original is broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IAsyncBusinessRule Not(this IAsyncBusinessRule rule)
        {
            return new NotAsyncBusinessRule(rule);
        }

        /// <summary>
        /// Creates a conditional async rule that only executes when the predicate returns true.
        /// If the condition is false, the rule passes automatically.
        /// </summary>
        /// <param name="rule">The async rule to conditionally execute.</param>
        /// <param name="condition">The condition that must be true for the rule to be evaluated.</param>
        /// <returns>A conditional async rule that wraps the original rule.</returns>
        public static IAsyncBusinessRule When(this IAsyncBusinessRule rule, Func<BusinessRuleContext, bool> condition)
        {
            return new ConditionalAsyncBusinessRule(rule, condition);
        }

        /// <summary>
        /// Creates a cached async rule that remembers its evaluation result for the specified duration.
        /// Subsequent evaluations within the cache duration return the cached result.
        /// </summary>
        /// <param name="rule">The async rule to cache.</param>
        /// <param name="duration">The duration to cache the result.</param>
        /// <returns>A cached async rule that wraps the original rule.</returns>
        public static IAsyncBusinessRule Cached(this IAsyncBusinessRule rule, TimeSpan duration)
        {
            return new CachedAsyncBusinessRule(rule, duration);
        }
    }
}
