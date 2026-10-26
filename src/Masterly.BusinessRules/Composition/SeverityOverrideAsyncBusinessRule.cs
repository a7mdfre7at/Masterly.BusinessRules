using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A decorator that reports the wrapped async rule with a different <see cref="Severity"/> without subclassing it.
    /// Every other metadata member (<see cref="Code"/>, <see cref="Message"/>, <see cref="Name"/>, <see cref="Description"/>,
    /// <see cref="Category"/>, <see cref="Tags"/>) and the verdict are forwarded unchanged; a broken result keeps the
    /// inner result's <see cref="BusinessRuleResult.Details"/> and carries the overridden severity.
    /// In an explanation tree the decorator appears as a <c>SEVERITY</c> node above the inner rule.
    /// Created through the <see cref="AsyncBusinessRuleSeverityExtensions.WithSeverity(IAsyncBusinessRule, RuleSeverity)"/> extension.
    /// </summary>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule softened = new UniqueEmailRule(email, repository).WithSeverity(RuleSeverity.Warning);
    /// await AsyncBusinessRuleChecker.CheckAllAsync(context, new[] { softened, strict }, new AsyncRuleEvaluationOptions { ThrowOn = RuleSeverity.Error }, ct);
    /// </code>
    /// </example>
    public sealed class SeverityOverrideAsyncBusinessRule : IDecoratedAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private readonly IAsyncBusinessRule _innerRule;
        private readonly RuleSeverity _severity;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeverityOverrideAsyncBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The async rule whose severity is overridden.</param>
        /// <param name="severity">The severity to report.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public SeverityOverrideAsyncBusinessRule(IAsyncBusinessRule innerRule, RuleSeverity severity)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _severity = severity;
        }

        /// <inheritdoc />
        public IAsyncBusinessRule InnerRule => _innerRule;

        /// <inheritdoc />
        public string Code => _innerRule.Code;

        /// <inheritdoc />
        public string Message => _innerRule.Message;

        /// <summary>
        /// Gets the overridden severity.
        /// </summary>
        public RuleSeverity Severity => _severity;

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
            return await EvaluateCoreAsync(context, null, cancellationToken) != null;
        }

        /// <inheritdoc />
        /// <remarks>A broken result carries the overridden severity and the inner result's details.</remarks>
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return EvaluateCoreAsync(context, null, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule is broken; the result carries the overridden severity.</exception>
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

            BusinessRuleResult? innerResult = await AsyncBusinessRuleWalker.EvaluateNodeAsync(_innerRule, context, children, cancellationToken);
            BusinessRuleResult? result = innerResult == null
                ? null
                : new BusinessRuleResult(innerResult.Code, innerResult.Message, _severity, innerResult.Details);

            if (trace != null && children != null)
            {
                trace.Add(AsyncBusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.SeverityOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return result;
        }
    }
}
