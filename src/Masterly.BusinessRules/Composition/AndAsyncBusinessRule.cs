using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Structural async node produced by <see cref="AsyncBusinessRuleExtensions.And"/>: broken only when <b>both</b> operands are broken.
    /// The first operand is evaluated first; when it passes the second operand is not evaluated (short-circuit).
    /// The context and cancellation token are forwarded to both operands.
    /// </summary>
    public sealed class AndAsyncBusinessRule : IComposedAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private const string CodeSeparator = "+";
        private const string MessageSeparator = " AND ";
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IAsyncBusinessRule _first;
        private readonly IAsyncBusinessRule _second;
        private readonly IReadOnlyList<IAsyncBusinessRule> _operands;

        /// <summary>
        /// Initializes a new <see cref="AndAsyncBusinessRule"/> over two operands.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <exception cref="ArgumentNullException">Thrown when either operand is null.</exception>
        public AndAsyncBusinessRule(IAsyncBusinessRule first, IAsyncBusinessRule second)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
            _operands = new[] { _first, _second };
        }

        /// <summary>
        /// Gets the synthesised code <c>A+B</c>.
        /// </summary>
        public string Code => $"{_first.Code}{CodeSeparator}{_second.Code}";

        /// <summary>
        /// Gets the synthesised message describing both operands.
        /// </summary>
        public string Message => $"{_first.Message}{MessageSeparator}{_second.Message}";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => GetType().Name;

        /// <inheritdoc />
        public string Description => "Broken only when both operands are broken.";

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <inheritdoc />
        public RuleOperator Operator => RuleOperator.And;

        /// <inheritdoc />
        public IReadOnlyList<IAsyncBusinessRule> Operands => _operands;

        /// <inheritdoc />
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => await EvaluateCoreAsync(context, null, cancellationToken) != null;

        /// <inheritdoc />
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => EvaluateCoreAsync(context, null, cancellationToken);

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when both operands are broken.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateCoreAsync(context, null, cancellationToken);
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        Task<BusinessRuleResult?> IExplainableAsyncBusinessRule.EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
            => EvaluateCoreAsync(context, trace, cancellationToken);

        private Task<BusinessRuleResult?> EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
            => AsyncBinaryRuleEvaluation.EvaluateAsync(this, context, trace, cancellationToken);
    }
}
