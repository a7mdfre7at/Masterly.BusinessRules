using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Subject-typed node produced by <see cref="TypedBusinessRuleExtensions.Not{TSubject}"/>: broken when its operand
    /// passes for the subject, and passing when the operand is broken. The subject and the context are forwarded to the operand.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class NotBusinessRule<TSubject> : IBusinessRule<TSubject>
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IBusinessRule<TSubject> _operand;
        private readonly IReadOnlyList<IBusinessRule<TSubject>> _operands;

        /// <summary>
        /// Initializes a new <see cref="NotBusinessRule{TSubject}"/> over one operand.
        /// </summary>
        /// <param name="operand">The rule to invert.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operand"/> is null.</exception>
        public NotBusinessRule(IBusinessRule<TSubject> operand)
        {
            _operand = operand ?? throw new ArgumentNullException(nameof(operand));
            _operands = new[] { _operand };
        }

        /// <summary>
        /// Gets the synthesised code <c>!A</c>.
        /// </summary>
        public string Code => $"{RuleCompositionText.NotCodePrefix}{_operand.Code}";

        /// <summary>
        /// Gets the synthesised message describing the inverted operand.
        /// </summary>
        public string Message => $"{RuleCompositionText.NotMessagePrefix}{_operand.Message}";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => nameof(NotBusinessRule<TSubject>);

        /// <inheritdoc />
        public string Description => RuleCompositionText.NotDescription;

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the operator that combines the operands' broken states.
        /// </summary>
        public RuleOperator Operator => RuleOperator.Not;

        /// <summary>
        /// Gets the single operand.
        /// </summary>
        public IReadOnlyList<IBusinessRule<TSubject>> Operands => _operands;

        /// <inheritdoc />
        public bool IsBroken(TSubject subject) => !_operand.IsBroken(subject);

        /// <inheritdoc />
        public bool IsBroken(TSubject subject, BusinessRuleContext context) => !_operand.IsBroken(subject, context);

        /// <inheritdoc />
        /// <remarks>A factory carrying the node's metadata.</remarks>
        public BusinessRuleResult Evaluate(TSubject subject) => RuleResultFactory.Create(this);

        /// <inheritdoc />
        /// <remarks>The context is not used: a structural node attaches no context-aware details of its own.</remarks>
        public BusinessRuleResult Evaluate(TSubject subject, BusinessRuleContext context) => Evaluate(subject);
    }
}
