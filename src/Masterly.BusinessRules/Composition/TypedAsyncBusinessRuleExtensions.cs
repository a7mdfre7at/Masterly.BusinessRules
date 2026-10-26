using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Extension methods for binding and composing subject-typed asynchronous business rules.
    /// </summary>
    public static class TypedAsyncBusinessRuleExtensions
    {
        /// <summary>
        /// Binds the typed async rule to a subject, returning an ordinary <see cref="IAsyncBusinessRule"/> that
        /// forwards all metadata and evaluates the rule against that subject. This is the only bridge from the
        /// subject-typed hierarchy to the non-generic one; the bound rule works with every decorator, operator and checker.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="rule">The typed async rule to bind.</param>
        /// <param name="subject">The subject to evaluate the rule against; passed to the rule as-is.</param>
        /// <returns>A <see cref="BoundAsyncBusinessRule{TSubject}"/> over the rule and subject.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static BoundAsyncBusinessRule<TSubject> For<TSubject>(this IAsyncBusinessRule<TSubject> rule, TSubject subject)
        {
            return new BoundAsyncBusinessRule<TSubject>(rule, subject);
        }

        /// <summary>
        /// Creates a typed async rule that is broken only when both rules are broken (logical AND over the broken state).
        /// The returned <see cref="AndAsyncBusinessRule{TSubject}"/> forwards the subject, the context and the
        /// cancellation token to both operands, short-circuits when the first operand passes and reports the
        /// synthesised code <c>A+B</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="first">The first async rule.</param>
        /// <param name="second">The second async rule.</param>
        /// <returns>A typed async rule that is broken when both input rules are broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either rule is null.</exception>
        public static IAsyncBusinessRule<TSubject> And<TSubject>(this IAsyncBusinessRule<TSubject> first, IAsyncBusinessRule<TSubject> second)
        {
            return new AndAsyncBusinessRule<TSubject>(first, second);
        }

        /// <summary>
        /// Creates a typed async rule that is broken when either rule is broken (logical OR over the broken state).
        /// The returned <see cref="OrAsyncBusinessRule{TSubject}"/> forwards the subject, the context and the
        /// cancellation token to both operands, short-circuits when the first operand is broken and reports the
        /// synthesised code <c>A|B</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="first">The first async rule.</param>
        /// <param name="second">The second async rule.</param>
        /// <returns>A typed async rule that is broken when either input rule is broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either rule is null.</exception>
        public static IAsyncBusinessRule<TSubject> Or<TSubject>(this IAsyncBusinessRule<TSubject> first, IAsyncBusinessRule<TSubject> second)
        {
            return new OrAsyncBusinessRule<TSubject>(first, second);
        }

        /// <summary>
        /// Creates a typed async rule that inverts the broken state of the original rule (logical NOT).
        /// The returned <see cref="NotAsyncBusinessRule{TSubject}"/> forwards the subject, the context and the
        /// cancellation token to its operand and reports the synthesised code <c>!A</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="rule">The async rule to invert.</param>
        /// <returns>A typed async rule that is broken when the original passes, and passes when the original is broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IAsyncBusinessRule<TSubject> Not<TSubject>(this IAsyncBusinessRule<TSubject> rule)
        {
            return new NotAsyncBusinessRule<TSubject>(rule);
        }
    }
}
