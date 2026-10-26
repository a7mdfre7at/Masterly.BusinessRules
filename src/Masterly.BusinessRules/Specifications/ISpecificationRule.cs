using System;
using System.Linq.Expressions;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A subject-typed rule whose verdict is an expression tree, so the same rule can be evaluated in memory
    /// (<see cref="IBusinessRule{TSubject}.IsBroken(TSubject)"/>) and translated by a LINQ provider
    /// (<see cref="SpecificationQueryableExtensions.WhereBroken{TSubject}"/> /
    /// <see cref="SpecificationQueryableExtensions.WhereSatisfies{TSubject}"/>). The polarity is explicit:
    /// <see cref="BrokenExpression"/> selects the subjects that violate the rule and <see cref="SatisfiedExpression"/>
    /// selects those that comply; there is no ambiguous <c>ToExpression()</c>. Both expressions are single-parameter
    /// lambdas and agree (<c>Satisfied == !Broken</c>) for every subject.
    /// </summary>
    /// <typeparam name="TSubject">The type of the subject the rule validates.</typeparam>
    public interface ISpecificationRule<TSubject> : IBusinessRule<TSubject>
    {
        /// <summary>
        /// Gets the predicate that is true for every subject that violates the rule. This is the expression a LINQ
        /// provider translates for <see cref="SpecificationQueryableExtensions.WhereBroken{TSubject}"/>. Implementations
        /// return the same instance on every call.
        /// </summary>
        Expression<Func<TSubject, bool>> BrokenExpression { get; }

        /// <summary>
        /// Gets the predicate that is true for every subject that complies with the rule: the logical negation of
        /// <see cref="BrokenExpression"/>. This is the expression a LINQ provider translates for
        /// <see cref="SpecificationQueryableExtensions.WhereSatisfies{TSubject}"/>. Implementations return the same
        /// instance on every call.
        /// </summary>
        Expression<Func<TSubject, bool>> SatisfiedExpression { get; }
    }
}
