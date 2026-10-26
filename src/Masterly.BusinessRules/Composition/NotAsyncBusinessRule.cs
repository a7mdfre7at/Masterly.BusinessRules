using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Structural async node produced by <see cref="AsyncBusinessRuleExtensions.Not"/>: broken when its operand passes and vice versa.
    /// The context and cancellation token are forwarded to the operand.
    /// </summary>
    public sealed class NotAsyncBusinessRule : IComposedAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IAsyncBusinessRule _operand;
        private readonly IReadOnlyList<IAsyncBusinessRule> _operands;

        /// <summary>
        /// Initializes a new <see cref="NotAsyncBusinessRule"/> over one operand.
        /// </summary>
        /// <param name="operand">The async rule to invert.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operand"/> is null.</exception>
        public NotAsyncBusinessRule(IAsyncBusinessRule operand)
        {
            _operand = operand ?? throw new ArgumentNullException(nameof(operand));
            _operands = new[] { _operand };
        }

        /// <summary>
        /// Gets the synthesised code <c>!A</c>.
        /// </summary>
        public string Code => $"{RuleCompositionText.NotCodePrefix}{_operand.Code}";

        /// <summary>
        /// Gets the synthesised message <c>NOT: ...</c>.
        /// </summary>
        public string Message => $"{RuleCompositionText.NotMessagePrefix}{_operand.Message}";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => GetType().Name;

        /// <inheritdoc />
        public string Description => RuleCompositionText.NotDescription;

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <inheritdoc />
        public RuleOperator Operator => RuleOperator.Not;

        /// <inheritdoc />
        public IReadOnlyList<IAsyncBusinessRule> Operands => _operands;

        /// <inheritdoc />
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => await EvaluateCoreAsync(context, null, cancellationToken) != null;

        /// <inheritdoc />
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => EvaluateCoreAsync(context, null, cancellationToken);

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the operand passes.</exception>
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

        private async Task<BusinessRuleResult?> EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            cancellationToken.ThrowIfCancellationRequested();
            BusinessRuleResult? operandResult = await AsyncBusinessRuleWalker.EvaluateNodeAsync(_operand, context, children, cancellationToken);
            bool isBroken = operandResult == null;

            if (trace != null && children != null)
            {
                trace.Add(AsyncBusinessRuleWalker.Evaluated(this, isBroken, RuleExplanationText.NotOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken ? RuleResultFactory.Create(this) : null;
        }
    }
}
