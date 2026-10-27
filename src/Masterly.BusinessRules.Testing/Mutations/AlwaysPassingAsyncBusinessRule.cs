using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// A smoke-mode mutant that never breaks and never evaluates the wrapped async rule: the rule has silently stopped
    /// firing. All seven metadata members are forwarded; a cancelled token is still honoured.
    /// Created through <see cref="RuleMutationExtensions.AlwaysPass(IAsyncBusinessRule)"/>.
    /// </summary>
    public sealed class AlwaysPassingAsyncBusinessRule : IExplainableDecoratedAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlwaysPassingAsyncBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The async rule that is silenced.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public AlwaysPassingAsyncBusinessRule(IAsyncBusinessRule innerRule)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
        }

        /// <inheritdoc />
        public IAsyncBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets the operator label of this decorator's explanation node.
        /// </summary>
        public string ExplanationOperator => RuleMutationText.AlwaysPassingOperator;

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
        /// <remarks>Always false; the inner rule is not evaluated.</remarks>
        public Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        /// <inheritdoc />
        /// <remarks>Always null; the inner rule is not evaluated.</remarks>
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<BusinessRuleResult?>(null);
        }

        /// <inheritdoc />
        /// <remarks>Never throws a validation exception; the inner rule is not evaluated.</remarks>
        public Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Passes without evaluating the wrapped rule, so <c>ExplainAsync</c> records it as not evaluated.
        /// </summary>
        /// <param name="evaluation">The evaluation of the wrapped rule, deliberately left unused.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public Task<BusinessRuleResult?> EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation evaluation, CancellationToken cancellationToken)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<BusinessRuleResult?>(null);
        }
    }
}
