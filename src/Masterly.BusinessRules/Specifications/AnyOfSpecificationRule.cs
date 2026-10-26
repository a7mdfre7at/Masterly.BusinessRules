using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Specification node produced by <see cref="SpecificationRuleExtensions.AnyOf{TSubject}"/>: satisfied when
    /// <b>any</b> operand is satisfied, hence broken only when <b>every</b> operand is broken. Its
    /// <see cref="ComposedSpecificationRule{TSubject}.BrokenExpression"/> is one lambda whose body is an <c>AndAlso</c>
    /// chain over the operands' broken predicates, spliced onto a single parameter. Reports the synthesised code
    /// <c>ANY(A,B)</c>.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class AnyOfSpecificationRule<TSubject> : ComposedSpecificationRule<TSubject>
    {
        /// <summary>
        /// Initializes a new <see cref="AnyOfSpecificationRule{TSubject}"/> over the operands.
        /// </summary>
        /// <param name="operands">The specifications of which at least one must be satisfied; at least one, none null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operands"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operands"/> is empty or contains a null operand.</exception>
        public AnyOfSpecificationRule(IEnumerable<ISpecificationRule<TSubject>> operands)
            : base(RuleOperator.Any, RequireOperands(operands, nameof(operands)))
        {
        }

        /// <summary>
        /// Gets the synthesised code <c>ANY(A,B)</c>.
        /// </summary>
        public override string Code => JoinCodes(SpecificationText.AnyOfCodePrefix);

        /// <summary>
        /// Gets the synthesised message listing every operand's message.
        /// </summary>
        public override string Message => JoinMessages(SpecificationText.AnyOfMessagePrefix, SpecificationText.AnyOfMessageSeparator);

        /// <inheritdoc />
        public override string Name => nameof(AnyOfSpecificationRule<TSubject>);

        /// <inheritdoc />
        public override string Description => SpecificationText.AnyOfDescription;

        private protected override Expression<Func<TSubject, bool>> BuildBrokenExpression()
        {
            return SpecificationExpressions.All(BrokenExpressionsOfOperands());
        }
    }
}
