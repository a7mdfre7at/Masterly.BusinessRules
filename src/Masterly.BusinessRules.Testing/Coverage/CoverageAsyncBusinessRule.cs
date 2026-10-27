using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// A decorator that records every verdict of the wrapped async rule into a <see cref="RuleCoverageSession"/> under
    /// the rule's <see cref="Code"/>. All seven metadata members are forwarded unchanged; the verdict is forwarded too.
    /// Outcomes are recorded from <see cref="IsBrokenAsync"/>, <see cref="EvaluateAsync"/> and <see cref="CheckAsync"/>.
    /// A rule that throws or is cancelled records nothing.
    /// Wrap the operands <em>before</em> composing them so operand outcomes (and short-circuited operands) are visible.
    /// Created through <see cref="RuleCoverageExtensions.WithCoverage(IAsyncBusinessRule, RuleCoverageSession)"/>.
    /// </summary>
    public sealed class CoverageAsyncBusinessRule : IExplainableDecoratedAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;
        private readonly RuleCoverageSession _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoverageAsyncBusinessRule"/> class and registers the inner
        /// rule's code with the session so it is reported even when never evaluated.
        /// </summary>
        /// <param name="innerRule">The async rule whose outcomes are recorded.</param>
        /// <param name="session">The session that receives the outcomes.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="session"/> is null.</exception>
        public CoverageAsyncBusinessRule(IAsyncBusinessRule innerRule, RuleCoverageSession session)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.Register(innerRule);
        }

        /// <inheritdoc />
        public IAsyncBusinessRule InnerRule => _innerRule;

        /// <summary>
        /// Gets the operator label of this decorator's explanation node.
        /// </summary>
        public string ExplanationOperator => RuleCoverageText.CoverageOperator;

        /// <summary>
        /// Gets the session the outcomes are recorded into.
        /// </summary>
        public RuleCoverageSession Session => _session;

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
            bool isBroken = await _innerRule.IsBrokenAsync(context, cancellationToken);
            _session.Record(_innerRule, isBroken);
            return isBroken;
        }

        /// <inheritdoc />
        public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await _innerRule.EvaluateAsync(context, cancellationToken);
            _session.Record(_innerRule, result != null);
            return result;
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule is broken; the inner rule's own exception propagates.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await _innerRule.CheckAsync(context, cancellationToken);
            }
            catch (BusinessRuleValidationException)
            {
                _session.Record(_innerRule, true);
                throw;
            }

            _session.Record(_innerRule, false);
        }

        /// <summary>
        /// Evaluates the wrapped rule once through <paramref name="evaluation"/>, records the outcome and forwards the verdict.
        /// </summary>
        /// <param name="evaluation">The one-shot evaluation of the wrapped rule supplied by the core walker.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The wrapped rule's broken result, or null when it passed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluation"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<BusinessRuleResult?> EvaluateDecoratedAsync(AsyncDecoratedRuleEvaluation evaluation, CancellationToken cancellationToken)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            BusinessRuleResult? result = await evaluation.EvaluateInnerAsync(cancellationToken);
            _session.Record(_innerRule, result != null);
            return result;
        }
    }
}
