using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A decorator that reports the wrapped rule with a different <see cref="Severity"/> without subclassing it.
    /// Every other metadata member (<see cref="Code"/>, <see cref="Message"/>, <see cref="Name"/>, <see cref="Description"/>,
    /// <see cref="Category"/>, <see cref="Tags"/>) and the verdict are forwarded unchanged; a broken result keeps the
    /// inner result's <see cref="BusinessRuleResult.Details"/> and carries the overridden severity.
    /// In an explanation tree the decorator appears as a <c>SEVERITY</c> node above the inner rule.
    /// Created through the <see cref="BusinessRuleSeverityExtensions.WithSeverity(IBusinessRule, RuleSeverity)"/> extension.
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule softened = new EmailFormatRule(email).WithSeverity(RuleSeverity.Warning);
    /// BusinessRuleChecker.CheckAll(new[] { softened, strict }, new RuleEvaluationOptions { ThrowOn = RuleSeverity.Error });
    /// </code>
    /// </example>
    public sealed class SeverityOverrideBusinessRule : IDecoratedBusinessRule, IExplainableBusinessRule
    {
        private readonly IBusinessRule _innerRule;
        private readonly RuleSeverity _severity;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeverityOverrideBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The rule whose severity is overridden.</param>
        /// <param name="severity">The severity to report.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> is null.</exception>
        public SeverityOverrideBusinessRule(IBusinessRule innerRule, RuleSeverity severity)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _severity = severity;
        }

        /// <inheritdoc />
        public IBusinessRule InnerRule => _innerRule;

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
        public bool IsBroken()
        {
            return EvaluateCore(null, null) != null;
        }

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context)
        {
            return EvaluateCore(context, null) != null;
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule is broken; the result carries the overridden severity.</exception>
        public void Check()
        {
            ThrowIfBroken(EvaluateCore(null, null));
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the inner rule is broken; the result carries the overridden severity.</exception>
        public void Check(BusinessRuleContext context)
        {
            ThrowIfBroken(EvaluateCore(context, null));
        }

        /// <inheritdoc />
        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private static void ThrowIfBroken(BusinessRuleResult? result)
        {
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            BusinessRuleResult? innerResult = BusinessRuleWalker.EvaluateNode(_innerRule, context, children);
            BusinessRuleResult? result = innerResult == null
                ? null
                : new BusinessRuleResult(innerResult.Code, innerResult.Message, _severity, innerResult.Details);

            if (trace != null && children != null)
            {
                trace.Add(BusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.SeverityOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return result;
        }
    }
}
