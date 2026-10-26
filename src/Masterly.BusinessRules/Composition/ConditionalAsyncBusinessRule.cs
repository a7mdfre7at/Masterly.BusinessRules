using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A wrapper that conditionally executes an async rule based on a predicate.
    /// When the condition returns false, the rule is considered not broken (passes).
    /// In an explanation tree an unmet condition is reported as <see cref="RuleOutcome.Skipped"/> with the inner rule
    /// as a <see cref="RuleOutcome.NotEvaluated"/> child.
    /// </summary>
    /// <remarks>
    /// The condition predicate receives the <see cref="BusinessRuleContext"/> allowing
    /// for context-aware conditional logic.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Only validate if feature flag is enabled
    /// ConditionalAsyncBusinessRule rule = new ConditionalAsyncBusinessRule(
    ///     new AsyncValidationRule(),
    ///     ctx => ctx.Get&lt;bool&gt;("featureEnabled")
    /// );
    /// </code>
    /// </example>
    public sealed class ConditionalAsyncBusinessRule : IDecoratedAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;
        private readonly Func<BusinessRuleContext, bool> _condition;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConditionalAsyncBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The async rule to conditionally execute.</param>
        /// <param name="condition">The condition that must be true for the rule to be evaluated. Receives the context.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="condition"/> is null.</exception>
        public ConditionalAsyncBusinessRule(IAsyncBusinessRule innerRule, Func<BusinessRuleContext, bool> condition)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        /// <inheritdoc />
        public IAsyncBusinessRule InnerRule => _innerRule;

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
        /// <remarks>Returns false (not broken) if the condition is false.</remarks>
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await EvaluateCoreAsync(context, null, cancellationToken) != null;
        }

        /// <inheritdoc />
        /// <remarks>Returns null if the condition is false.</remarks>
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return EvaluateCoreAsync(context, null, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>Does nothing if the condition is false.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when condition is true and the inner rule is broken.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (_condition(context))
                await _innerRule.CheckAsync(context, cancellationToken);
        }

        Task<BusinessRuleResult?> IExplainableAsyncBusinessRule.EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
            => EvaluateCoreAsync(context, trace, cancellationToken);

        private async Task<BusinessRuleResult?> EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            if (!_condition(context))
            {
                trace?.Add(AsyncBusinessRuleWalker.Skipped(this, _innerRule, RuleExplanationText.WhenOperator, RuleExplanationText.PreconditionNotMetReason));
                return null;
            }

            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            BusinessRuleResult? result = await AsyncBusinessRuleWalker.EvaluateNodeAsync(_innerRule, context, children, cancellationToken);

            if (trace != null && children != null)
            {
                trace.Add(AsyncBusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.WhenOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return result;
        }
    }
}
