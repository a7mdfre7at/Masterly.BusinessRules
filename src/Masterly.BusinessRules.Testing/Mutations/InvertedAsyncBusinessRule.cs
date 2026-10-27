using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// A smoke-mode mutant that is broken exactly when the wrapped async rule passes. Unlike <c>Not()</c> it keeps the
    /// rule's <see cref="Code"/>, <see cref="Message"/> and every other metadata member, so the test suite sees the same
    /// rule identity misbehaving. Created through <see cref="RuleMutationExtensions.Invert(IAsyncBusinessRule)"/>.
    /// </summary>
    public sealed class InvertedAsyncBusinessRule : IExplainableDecoratedAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvertedAsyncBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The async rule whose verdict is inverted.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public InvertedAsyncBusinessRule(IAsyncBusinessRule innerRule)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
        }

        /// <inheritdoc />
        public IAsyncBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets the operator label of this decorator's explanation node.
        /// </summary>
        public string ExplanationOperator => RuleMutationText.InvertedOperator;

        /// <inheritdoc />
        public string Code => _innerRule.Code;

        /// <inheritdoc />
        public string Message => _innerRule.Message;

        /// <inheritdoc />
        public RuleSeverity Severity => _innerRule.Severity;

        /// <inheritdoc />
        public string Name => _innerRule.Name;

        /// <inheritdoc />
        public string Description => _innerRule.Description;

        /// <inheritdoc />
        public string Category => _innerRule.Category;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => _innerRule.Tags;

        /// <inheritdoc />
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return !await _innerRule.IsBrokenAsync(context, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>The result is built from the inverted rule's own metadata because the inner rule produces none when it passes.</remarks>
        public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            bool isBroken = await IsBrokenAsync(context, cancellationToken);
            return isBroken ? CreateResult() : null;
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule passes.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateAsync(context, cancellationToken);
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        /// <summary>
        /// Evaluates the wrapped rule once through <paramref name="evaluation"/> and inverts its verdict.
        /// </summary>
        /// <param name="evaluation">The one-shot evaluation of the wrapped rule supplied by the core walker.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The mutant's broken result when the wrapped rule passed; null when it was broken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<BusinessRuleResult?> EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation evaluation, CancellationToken cancellationToken)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            BusinessRuleResult? inner = await evaluation.EvaluateInnerAsync(cancellationToken);
            return inner == null ? CreateResult() : null;
        }

        private BusinessRuleResult CreateResult() => new BusinessRuleResult(Code, Message, Severity, Name, Category, Tags);
    }
}
