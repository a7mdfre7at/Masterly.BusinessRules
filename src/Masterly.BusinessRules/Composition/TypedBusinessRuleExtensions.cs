using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Extension methods for binding and composing subject-typed synchronous business rules.
    /// </summary>
    public static class TypedBusinessRuleExtensions
    {
        /// <summary>
        /// Binds the typed rule to a subject, returning an ordinary <see cref="IBusinessRule"/> that forwards all
        /// metadata and evaluates the rule against that subject. This is the only bridge from the subject-typed
        /// hierarchy to the non-generic one; the bound rule works with every decorator, operator and checker.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="rule">The typed rule to bind.</param>
        /// <param name="subject">The subject to evaluate the rule against; passed to the rule as-is.</param>
        /// <returns>A <see cref="BoundBusinessRule{TSubject}"/> over the rule and subject.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static BoundBusinessRule<TSubject> For<TSubject>(this IBusinessRule<TSubject> rule, TSubject subject)
        {
            return new BoundBusinessRule<TSubject>(rule, subject);
        }

        /// <summary>
        /// Adapts the typed sync rule to the typed async hierarchy without binding a subject, so it can join typed async
        /// compositions (<c>asyncRule.And(syncRule.ToAsync())</c>) and <c>IEnumerable&lt;IAsyncBusinessRule&lt;T&gt;&gt;</c>
        /// batches. A specification rule binds to the more specific <see cref="SpecificationRuleExtensions.ToAsync{TSubject}"/>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="rule">The typed sync rule to adapt.</param>
        /// <returns>A <see cref="SyncToAsyncRuleAdapter{TSubject}"/> over the rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IAsyncBusinessRule<TSubject> ToAsync<TSubject>(this IBusinessRule<TSubject> rule)
        {
            return new SyncToAsyncRuleAdapter<TSubject>(rule);
        }

        /// <summary>
        /// Creates a typed rule that is broken only when both rules are broken (logical AND over the broken state).
        /// The returned <see cref="AndBusinessRule{TSubject}"/> forwards the subject and the context to both operands,
        /// short-circuits when the first operand passes and reports the synthesised code <c>A+B</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="first">The first rule.</param>
        /// <param name="second">The second rule.</param>
        /// <returns>A typed rule that is broken when both input rules are broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either rule is null.</exception>
        public static IBusinessRule<TSubject> And<TSubject>(this IBusinessRule<TSubject> first, IBusinessRule<TSubject> second)
        {
            return new AndBusinessRule<TSubject>(first, second);
        }

        /// <summary>
        /// Creates a typed rule that is broken when either rule is broken (logical OR over the broken state).
        /// The returned <see cref="OrBusinessRule{TSubject}"/> forwards the subject and the context to both operands,
        /// short-circuits when the first operand is broken and reports the synthesised code <c>A|B</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="first">The first rule.</param>
        /// <param name="second">The second rule.</param>
        /// <returns>A typed rule that is broken when either input rule is broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either rule is null.</exception>
        public static IBusinessRule<TSubject> Or<TSubject>(this IBusinessRule<TSubject> first, IBusinessRule<TSubject> second)
        {
            return new OrBusinessRule<TSubject>(first, second);
        }

        /// <summary>
        /// Creates a typed rule that inverts the broken state of the original rule (logical NOT).
        /// The returned <see cref="NotBusinessRule{TSubject}"/> forwards the subject and the context to its operand and
        /// reports the synthesised code <c>!A</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="rule">The rule to invert.</param>
        /// <returns>A typed rule that is broken when the original passes, and passes when the original is broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IBusinessRule<TSubject> Not<TSubject>(this IBusinessRule<TSubject> rule)
        {
            return new NotBusinessRule<TSubject>(rule);
        }
    }
}
