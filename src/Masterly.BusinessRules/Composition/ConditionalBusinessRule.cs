using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A wrapper that conditionally executes a rule based on a predicate.
    /// When the condition returns false, the rule is considered not broken (passes).
    /// In an explanation tree an unmet condition is reported as <see cref="RuleOutcome.Skipped"/> with the inner rule
    /// as a <see cref="RuleOutcome.NotEvaluated"/> child.
    /// </summary>
    /// <example>
    /// <code>
    /// // Only validate age if user is not an admin
    /// ConditionalBusinessRule rule = new ConditionalBusinessRule(
    ///     new AgeValidationRule(age),
    ///     () => !isAdmin
    /// );
    /// </code>
    /// </example>
    public sealed class ConditionalBusinessRule : IDecoratedBusinessRule, IExplainableBusinessRule
    {
        private readonly IBusinessRule _innerRule;
        private readonly Func<bool> _condition;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConditionalBusinessRule"/> class.
        /// </summary>
        /// <param name="innerRule">The rule to conditionally execute.</param>
        /// <param name="condition">The condition that must be true for the rule to be evaluated.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerRule"/> or <paramref name="condition"/> is null.</exception>
        public ConditionalBusinessRule(IBusinessRule innerRule, Func<bool> condition)
        {
            _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        /// <inheritdoc />
        public IBusinessRule InnerRule => _innerRule;

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
        public bool IsBroken()
        {
            return EvaluateCore(null, null) != null;
        }

        /// <inheritdoc />
        /// <remarks>Returns false (not broken) if the condition is false.</remarks>
        public bool IsBroken(BusinessRuleContext context)
        {
            return EvaluateCore(context, null) != null;
        }

        /// <inheritdoc />
        /// <remarks>Does nothing if the condition is false.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when condition is true and the inner rule is broken.</exception>
        public void Check()
        {
            if (_condition())
                _innerRule.Check();
        }

        /// <inheritdoc />
        /// <remarks>Does nothing if the condition is false.</remarks>
        /// <exception cref="BusinessRuleValidationException">Thrown when condition is true and the inner rule is broken.</exception>
        public void Check(BusinessRuleContext context)
        {
            if (_condition())
                _innerRule.Check(context);
        }

        /// <inheritdoc />
        /// <remarks>Forwards to the inner rule, so its metadata and <c>Describe</c> details are kept.</remarks>
        public BusinessRuleResult Evaluate()
        {
            return _innerRule.Evaluate();
        }

        /// <inheritdoc />
        /// <remarks>Forwards to the inner rule, so its metadata and context-aware <c>Describe</c> details are kept.</remarks>
        public BusinessRuleResult Evaluate(BusinessRuleContext context)
        {
            return _innerRule.Evaluate(context);
        }

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            if (!_condition())
            {
                trace?.Add(BusinessRuleWalker.Skipped(this, _innerRule, RuleExplanationText.WhenOperator, RuleExplanationText.PreconditionNotMetReason));
                return null;
            }

            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            BusinessRuleResult? result = BusinessRuleWalker.EvaluateNode(_innerRule, context, children);

            if (trace != null && children != null)
            {
                trace.Add(BusinessRuleWalker.Evaluated(this, result != null, RuleExplanationText.WhenOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return result;
        }
    }
}
