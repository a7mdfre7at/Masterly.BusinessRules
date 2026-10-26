using System;
using System.Linq.Expressions;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Specification node produced by <see cref="SpecificationRuleExtensions.Not{TSubject}"/>: satisfied when its
    /// operand is broken and broken when its operand is satisfied. Its
    /// <see cref="ComposedSpecificationRule{TSubject}.BrokenExpression"/> is the operand's satisfied predicate and its
    /// <see cref="ComposedSpecificationRule{TSubject}.SatisfiedExpression"/> is the operand's broken predicate, so a
    /// double negation never piles up <c>Not</c> nodes. Reports the synthesised code <c>!A</c>.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class NotSpecificationRule<TSubject> : ComposedSpecificationRule<TSubject>
    {
        private readonly ISpecificationRule<TSubject> _operand;

        /// <summary>
        /// Initializes a new <see cref="NotSpecificationRule{TSubject}"/> over one operand.
        /// </summary>
        /// <param name="operand">The specification to invert.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operand"/> is null.</exception>
        public NotSpecificationRule(ISpecificationRule<TSubject> operand)
            : base(RuleOperator.Not, new[] { operand ?? throw new ArgumentNullException(nameof(operand)) })
        {
            _operand = operand;
        }

        /// <summary>
        /// Gets the synthesised code <c>!A</c>.
        /// </summary>
        public override string Code => $"{RuleCompositionText.NotCodePrefix}{_operand.Code}";

        /// <summary>
        /// Gets the synthesised message describing the inverted operand.
        /// </summary>
        public override string Message => $"{RuleCompositionText.NotMessagePrefix}{_operand.Message}";

        /// <inheritdoc />
        public override string Name => nameof(NotSpecificationRule<TSubject>);

        /// <inheritdoc />
        public override string Description => SpecificationText.NotDescription;

        private protected override Expression<Func<TSubject, bool>> BuildBrokenExpression()
        {
            return SpecificationExpressions.RequireSatisfied(_operand);
        }

        private protected override Expression<Func<TSubject, bool>> BuildSatisfiedExpression()
        {
            return SpecificationExpressions.RequireBroken(_operand);
        }
    }
}
