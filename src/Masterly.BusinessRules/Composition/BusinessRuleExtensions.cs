using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Extension methods for composing and transforming synchronous business rules.
    /// </summary>
    public static class BusinessRuleExtensions
    {
        /// <summary>
        /// Creates a new rule that is broken only when both rules are broken (logical AND).
        /// </summary>
        /// <param name="first">The first rule.</param>
        /// <param name="second">The second rule.</param>
        /// <returns>A new rule that is broken when both input rules are broken.</returns>
        public static IBusinessRule And(this IBusinessRule first, IBusinessRule second)
        {
            return new SimpleBusinessRule(
                () => first.IsBroken() && second.IsBroken(),
                $"Both rules must be satisfied: {first.Message} AND {second.Message}",
                $"{first.Code}+{second.Code}"
            );
        }

        /// <summary>
        /// Creates a new rule that is broken when either rule is broken (logical OR).
        /// </summary>
        /// <param name="first">The first rule.</param>
        /// <param name="second">The second rule.</param>
        /// <returns>A new rule that is broken when either input rule is broken.</returns>
        public static IBusinessRule Or(this IBusinessRule first, IBusinessRule second)
        {
            return new SimpleBusinessRule(
                () => first.IsBroken() || second.IsBroken(),
                $"At least one rule must be satisfied: {first.Message} OR {second.Message}",
                $"{first.Code}|{second.Code}"
            );
        }

        /// <summary>
        /// Creates a new rule that inverts the result of the original rule (logical NOT).
        /// </summary>
        /// <param name="rule">The rule to invert.</param>
        /// <returns>A new rule that is broken when the original rule passes, and passes when the original is broken.</returns>
        public static IBusinessRule Not(this IBusinessRule rule)
        {
            return new SimpleBusinessRule(
                () => !rule.IsBroken(),
                $"NOT: {rule.Message}",
                $"!{rule.Code}"
            );
        }

        /// <summary>
        /// Wraps a synchronous business rule as an async rule.
        /// </summary>
        /// <param name="rule">The synchronous rule to wrap.</param>
        /// <returns>An async rule adapter that wraps the synchronous rule.</returns>
        public static IAsyncBusinessRule ToAsync(this IBusinessRule rule)
        {
            return new SyncToAsyncRuleAdapter(rule);
        }

        /// <summary>
        /// Creates a conditional rule that only executes when the predicate returns true.
        /// If the condition is false, the rule passes automatically.
        /// </summary>
        /// <param name="rule">The rule to conditionally execute.</param>
        /// <param name="condition">The condition that must be true for the rule to be evaluated.</param>
        /// <returns>A conditional rule that wraps the original rule.</returns>
        public static IBusinessRule When(this IBusinessRule rule, Func<bool> condition)
        {
            return new ConditionalBusinessRule(rule, condition);
        }

        /// <summary>
        /// Creates a cached rule that remembers its evaluation result for the specified duration.
        /// Subsequent evaluations within the cache duration return the cached result.
        /// </summary>
        /// <param name="rule">The rule to cache.</param>
        /// <param name="duration">The duration to cache the result.</param>
        /// <returns>A cached rule that wraps the original rule.</returns>
        public static CachedBusinessRule Cached(this IBusinessRule rule, TimeSpan duration)
        {
            return new CachedBusinessRule(rule, duration);
        }
    }
}