using System;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Translates specification rules into <see cref="IQueryable{T}"/> filters. Both methods accept <b>only</b> an
    /// <see cref="ISpecificationRule{TSubject}"/>: a specification combined with the typed
    /// <c>.And()</c>/<c>.Or()</c>/<c>.Not()</c>, or bound with <c>.For(subject)</c> (and any <c>.Cached()</c>,
    /// <c>.When()</c>, <c>.WithSeverity()</c> or <c>.GatedBy()</c> applied to the bound rule), is an ordinary rule and
    /// does not compile here, so a composition cannot silently lose its translatability. Each method appends a
    /// single <c>Where</c> call whose predicate is the rule's own lambda, so the provider sees exactly the expression the
    /// rule evaluates in memory.
    /// </summary>
    public static class SpecificationQueryableExtensions
    {
        /// <summary>
        /// Filters the source to the subjects that <b>violate</b> the rule: <c>source.Where(rule.BrokenExpression)</c>.
        /// </summary>
        /// <typeparam name="TSubject">The element type of the query and the rule's subject type.</typeparam>
        /// <param name="source">The query to filter.</param>
        /// <param name="rule">The specification whose broken predicate is translated.</param>
        /// <returns>A query yielding the elements for which <see cref="ISpecificationRule{TSubject}.BrokenExpression"/> is true.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="rule"/> is null.</exception>
        /// <exception cref="RuleConfigurationException">Thrown when the rule exposes a null expression.</exception>
        public static IQueryable<TSubject> WhereBroken<TSubject>(this IQueryable<TSubject> source, ISpecificationRule<TSubject> rule)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (rule is null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return source.Where(SpecificationExpressions.RequireBroken(rule));
        }

        /// <summary>
        /// Filters the source to the subjects that <b>comply</b> with the rule: <c>source.Where(rule.SatisfiedExpression)</c>,
        /// which is <c>NOT(brokenExpression)</c>.
        /// </summary>
        /// <typeparam name="TSubject">The element type of the query and the rule's subject type.</typeparam>
        /// <param name="source">The query to filter.</param>
        /// <param name="rule">The specification whose satisfied predicate is translated.</param>
        /// <returns>A query yielding the elements for which <see cref="ISpecificationRule{TSubject}.SatisfiedExpression"/> is true.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="rule"/> is null.</exception>
        /// <exception cref="RuleConfigurationException">Thrown when the rule exposes a null expression.</exception>
        public static IQueryable<TSubject> WhereSatisfies<TSubject>(this IQueryable<TSubject> source, ISpecificationRule<TSubject> rule)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (rule is null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return source.Where(SpecificationExpressions.RequireSatisfied(rule));
        }
    }
}
