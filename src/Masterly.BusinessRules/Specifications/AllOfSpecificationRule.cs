using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Specification node produced by <see cref="SpecificationRuleExtensions.AllOf{TSubject}"/>: satisfied when
    /// <b>every</b> operand is satisfied, hence broken when <b>any</b> operand is broken. Its
    /// <see cref="ComposedSpecificationRule{TSubject}.BrokenExpression"/> is one lambda whose body is an <c>OrElse</c>
    /// chain over the operands' broken predicates, spliced onto a single parameter. Reports the synthesised code
    /// <c>ALL(A,B)</c>.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class AllOfSpecificationRule<TSubject> : ComposedSpecificationRule<TSubject>
    {
        /// <summary>
        /// Initializes a new <see cref="AllOfSpecificationRule{TSubject}"/> over the operands.
        /// </summary>
        /// <param name="operands">The specifications that must all be satisfied; at least one, none null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operands"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operands"/> is empty or contains a null operand.</exception>
        public AllOfSpecificationRule(IEnumerable<ISpecificationRule<TSubject>> operands)
            : base(RuleOperator.All, RequireOperands(operands, nameof(operands)))
        {
        }

        /// <summary>
        /// Gets the synthesised code <c>ALL(A,B)</c>.
        /// </summary>
        public override string Code => JoinCodes(SpecificationText.AllOfCodePrefix);

        /// <summary>
        /// Gets the synthesised message listing every operand's message.
        /// </summary>
        public override string Message => JoinMessages(SpecificationText.AllOfMessagePrefix, SpecificationText.AllOfMessageSeparator);

        /// <inheritdoc />
        public override string Name => nameof(AllOfSpecificationRule<TSubject>);

        /// <inheritdoc />
        public override string Description => SpecificationText.AllOfDescription;

        private protected override Expression<Func<TSubject, bool>> BuildBrokenExpression()
        {
            return SpecificationExpressions.Any(BrokenExpressionsOfOperands());
        }
    }
}
