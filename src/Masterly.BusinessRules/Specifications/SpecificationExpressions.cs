using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Builds the expression trees behind specification rules: negation and the N-ary <c>AndAlso</c>/<c>OrElse</c>
    /// combinations used by the <c>AllOf</c>/<c>AnyOf</c> combinators. Every result is a single lambda over one
    /// parameter whose body contains no <see cref="InvocationExpression"/>, so it stays translatable.
    /// </summary>
    internal static class SpecificationExpressions
    {
        /// <summary>
        /// Returns a lambda over the same parameter whose body is the logical negation of the predicate's body.
        /// </summary>
        public static Expression<Func<TSubject, bool>> Negate<TSubject>(Expression<Func<TSubject, bool>> predicate)
        {
            return Expression.Lambda<Func<TSubject, bool>>(Expression.Not(predicate.Body), predicate.Parameters);
        }

        /// <summary>
        /// Returns a single lambda that is true when any predicate is true (<c>OrElse</c> chain over the spliced bodies).
        /// </summary>
        public static Expression<Func<TSubject, bool>> Any<TSubject>(IReadOnlyList<Expression<Func<TSubject, bool>>> predicates)
        {
            return Combine(predicates, Expression.OrElse);
        }

        /// <summary>
        /// Returns a single lambda that is true when every predicate is true (<c>AndAlso</c> chain over the spliced bodies).
        /// </summary>
        public static Expression<Func<TSubject, bool>> All<TSubject>(IReadOnlyList<Expression<Func<TSubject, bool>>> predicates)
        {
            return Combine(predicates, Expression.AndAlso);
        }

        /// <summary>
        /// Returns the rule's <see cref="ISpecificationRule{TSubject}.BrokenExpression"/>, rejecting a null expression.
        /// </summary>
        /// <exception cref="RuleConfigurationException">Thrown when the rule returns a null expression.</exception>
        public static Expression<Func<TSubject, bool>> RequireBroken<TSubject>(ISpecificationRule<TSubject> rule)
        {
            Expression<Func<TSubject, bool>> expression = rule.BrokenExpression;
            if (expression is null)
            {
                throw new RuleConfigurationException(rule.Code, SpecificationText.MissingBrokenExpression);
            }

            return expression;
        }

        /// <summary>
        /// Returns the rule's <see cref="ISpecificationRule{TSubject}.SatisfiedExpression"/>, rejecting a null expression.
        /// </summary>
        /// <exception cref="RuleConfigurationException">Thrown when the rule returns a null expression.</exception>
        public static Expression<Func<TSubject, bool>> RequireSatisfied<TSubject>(ISpecificationRule<TSubject> rule)
        {
            Expression<Func<TSubject, bool>> expression = rule.SatisfiedExpression;
            if (expression is null)
            {
                throw new RuleConfigurationException(rule.Code, SpecificationText.MissingSatisfiedExpression);
            }

            return expression;
        }

        private static Expression<Func<TSubject, bool>> Combine<TSubject>(
            IReadOnlyList<Expression<Func<TSubject, bool>>> predicates,
            Func<Expression, Expression, BinaryExpression> combine)
        {
            Expression<Func<TSubject, bool>> first = predicates[0];
            ParameterExpression parameter = first.Parameters[0];
            Expression body = first.Body;

            for (int index = 1; index < predicates.Count; index++)
            {
                Expression<Func<TSubject, bool>> next = predicates[index];
                Expression spliced = ParameterReplacer.Replace(next.Body, next.Parameters[0], parameter);
                body = combine(body, spliced);
            }

            return Expression.Lambda<Func<TSubject, bool>>(body, parameter);
        }
    }
}
