using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Subject-typed async node produced by <see cref="TypedAsyncBusinessRuleExtensions.Or{TSubject}"/>: broken when
    /// <b>either</b> operand is broken for the subject. The first operand is evaluated first; when it is broken the second
    /// operand is not evaluated (short-circuit). The subject, the context and the cancellation token are forwarded to both operands.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class OrAsyncBusinessRule<TSubject> : IAsyncBusinessRule<TSubject>
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IAsyncBusinessRule<TSubject> _first;
        private readonly IAsyncBusinessRule<TSubject> _second;
        private readonly IReadOnlyList<IAsyncBusinessRule<TSubject>> _operands;

        /// <summary>
        /// Initializes a new <see cref="OrAsyncBusinessRule{TSubject}"/> over two operands.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <exception cref="ArgumentNullException">Thrown when either operand is null.</exception>
        public OrAsyncBusinessRule(IAsyncBusinessRule<TSubject> first, IAsyncBusinessRule<TSubject> second)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
            _operands = new[] { _first, _second };
        }

        /// <summary>
        /// Gets the synthesised code <c>A|B</c>.
        /// </summary>
        public string Code => $"{_first.Code}{RuleCompositionText.OrCodeSeparator}{_second.Code}";

        /// <summary>
        /// Gets the synthesised message describing both operands, prefixed like the sync twin.
        /// </summary>
        public string Message => $"{RuleCompositionText.OrMessagePrefix}{_first.Message}{RuleCompositionText.OrMessageSeparator}{_second.Message}";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => nameof(OrAsyncBusinessRule<TSubject>);

        /// <inheritdoc />
        public string Description => RuleCompositionText.OrDescription;

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the operator that combines the operands' broken states.
        /// </summary>
        public RuleOperator Operator => RuleOperator.Or;

        /// <summary>
        /// Gets the two operands in declaration order.
        /// </summary>
        public IReadOnlyList<IAsyncBusinessRule<TSubject>> Operands => _operands;

        /// <inheritdoc />
        public Task<bool> IsBrokenAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
            => SubjectRuleEvaluation.IsBinaryBrokenAsync(isOr: true, _first, _second, subject, context, cancellationToken);

        /// <inheritdoc />
        public async Task<BusinessRuleResult?> EvaluateAsync(TSubject subject, BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await IsBrokenAsync(subject, context, cancellationToken) ? RuleResultFactory.Create(this) : null;
        }
    }
}
