using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Subject-typed node produced by <see cref="TypedBusinessRuleExtensions.And{TSubject}"/>: broken only when <b>both</b>
    /// operands are broken for the subject. The first operand is evaluated first; when it passes the second operand is
    /// not evaluated (short-circuit). The subject and the context are forwarded to both operands.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class AndBusinessRule<TSubject> : IBusinessRule<TSubject>
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IBusinessRule<TSubject> _first;
        private readonly IBusinessRule<TSubject> _second;
        private readonly IReadOnlyList<IBusinessRule<TSubject>> _operands;

        /// <summary>
        /// Initializes a new <see cref="AndBusinessRule{TSubject}"/> over two operands.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <exception cref="ArgumentNullException">Thrown when either operand is null.</exception>
        public AndBusinessRule(IBusinessRule<TSubject> first, IBusinessRule<TSubject> second)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
            _operands = new[] { _first, _second };
        }

        /// <summary>
        /// Gets the synthesised code <c>A+B</c>.
        /// </summary>
        public string Code => $"{_first.Code}{RuleCompositionText.AndCodeSeparator}{_second.Code}";

        /// <summary>
        /// Gets the synthesised message describing both operands.
        /// </summary>
        public string Message => $"{RuleCompositionText.AndMessagePrefix}{_first.Message}{RuleCompositionText.AndMessageSeparator}{_second.Message}";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => nameof(AndBusinessRule<TSubject>);

        /// <inheritdoc />
        public string Description => RuleCompositionText.AndDescription;

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the operator that combines the operands' broken states.
        /// </summary>
        public RuleOperator Operator => RuleOperator.And;

        /// <summary>
        /// Gets the two operands in declaration order.
        /// </summary>
        public IReadOnlyList<IBusinessRule<TSubject>> Operands => _operands;

        /// <inheritdoc />
        public bool IsBroken(TSubject subject) => SubjectRuleEvaluation.IsBinaryBroken(isOr: false, _first, _second, subject, null);

        /// <inheritdoc />
        public bool IsBroken(TSubject subject, BusinessRuleContext context) => SubjectRuleEvaluation.IsBinaryBroken(isOr: false, _first, _second, subject, context);

        /// <inheritdoc />
        /// <remarks>A factory carrying the node's metadata.</remarks>
        public BusinessRuleResult Evaluate(TSubject subject) => RuleResultFactory.Create(this);

        /// <inheritdoc />
        /// <remarks>The context is not used: a structural node attaches no context-aware details of its own.</remarks>
        public BusinessRuleResult Evaluate(TSubject subject, BusinessRuleContext context) => Evaluate(subject);
    }
}
