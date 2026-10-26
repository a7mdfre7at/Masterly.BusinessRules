using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Combinators for <see cref="ISpecificationRule{TSubject}"/> stated in terms of <b>satisfaction</b>, matching the
    /// Specification-pattern vocabulary (<c>AllOf</c>, <c>AnyOf</c>, <c>Not</c>) rather than the broken-state algebra of
    /// the typed <c>And</c>/<c>Or</c>. Every combinator returns another specification, so the result stays translatable
    /// by <see cref="SpecificationQueryableExtensions.WhereBroken{TSubject}"/> and
    /// <see cref="SpecificationQueryableExtensions.WhereSatisfies{TSubject}"/>, and <see cref="ToAsync{TSubject}"/> is
    /// the minimal async mirror.
    /// </summary>
    public static class SpecificationRuleExtensions
    {
        /// <summary>
        /// Creates a specification satisfied only when <paramref name="first"/> and every rule in <paramref name="others"/>
        /// are satisfied, hence broken when any of them is broken. The combined broken predicate is a single lambda
        /// (<c>OrElse</c> chain, no <c>Invoke</c> nodes) with the synthesised code <c>ALL(A,B)</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="first">The first specification.</param>
        /// <param name="others">Further specifications; may be empty.</param>
        /// <returns>An <see cref="AllOfSpecificationRule{TSubject}"/> over all the operands.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="first"/> or <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="others"/> contains a null specification.</exception>
        public static ISpecificationRule<TSubject> AllOf<TSubject>(this ISpecificationRule<TSubject> first, params ISpecificationRule<TSubject>[] others)
        {
            return new AllOfSpecificationRule<TSubject>(Concat(first, others));
        }

        /// <summary>
        /// Creates a specification satisfied when <paramref name="first"/> or any rule in <paramref name="others"/> is
        /// satisfied, hence broken only when all of them are broken. The combined broken predicate is a single lambda
        /// (<c>AndAlso</c> chain, no <c>Invoke</c> nodes) with the synthesised code <c>ANY(A,B)</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="first">The first specification.</param>
        /// <param name="others">Further specifications; may be empty.</param>
        /// <returns>An <see cref="AnyOfSpecificationRule{TSubject}"/> over all the operands.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="first"/> or <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="others"/> contains a null specification.</exception>
        public static ISpecificationRule<TSubject> AnyOf<TSubject>(this ISpecificationRule<TSubject> first, params ISpecificationRule<TSubject>[] others)
        {
            return new AnyOfSpecificationRule<TSubject>(Concat(first, others));
        }

        /// <summary>
        /// Creates a specification satisfied when <paramref name="rule"/> is broken and broken when it is satisfied. The
        /// broken predicate is the operand's satisfied predicate (and vice versa), with the synthesised code <c>!A</c>.
        /// This overload takes precedence over the typed <c>IBusinessRule&lt;T&gt;.Not()</c> for a specification, so the
        /// result stays translatable.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="rule">The specification to invert.</param>
        /// <returns>A <see cref="NotSpecificationRule{TSubject}"/> over the operand.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static ISpecificationRule<TSubject> Not<TSubject>(this ISpecificationRule<TSubject> rule)
        {
            if (rule is null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return new NotSpecificationRule<TSubject>(rule);
        }

        /// <summary>
        /// Adapts the specification to the subject-typed async hierarchy so it can join async batches, async groups and
        /// <c>AsyncBusinessRuleChecker</c> calls. The adapter forwards all metadata and both expressions and completes
        /// synchronously. This is the whole async mirror of specification rules: an expression has no asynchronous form.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="rule">The specification to adapt.</param>
        /// <returns>An <see cref="AsyncSpecificationRuleAdapter{TSubject}"/> over the rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static IAsyncBusinessRule<TSubject> ToAsync<TSubject>(this ISpecificationRule<TSubject> rule)
        {
            return new AsyncSpecificationRuleAdapter<TSubject>(rule);
        }

        private static IEnumerable<ISpecificationRule<TSubject>> Concat<TSubject>(ISpecificationRule<TSubject> first, ISpecificationRule<TSubject>[] others)
        {
            if (first is null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (others is null)
            {
                throw new ArgumentNullException(nameof(others));
            }

            List<ISpecificationRule<TSubject>> operands = new List<ISpecificationRule<TSubject>>(others.Length + 1) { first };
            operands.AddRange(others);
            return operands;
        }
    }
}
