using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A composite business rule that contains and evaluates multiple child rules.
    /// The composite is considered broken if any of its child rules are broken; it is the
    /// <see cref="RuleOperator.All"/> node of the composition model and evaluates every child
    /// (no short-circuit) so that every broken child is reported.
    /// </summary>
    /// <example>
    /// <code>
    /// CompositeBusinessRule composite = new CompositeBusinessRule(new[]
    /// {
    ///     new AgeValidationRule(age),
    ///     new NameRequiredRule(name)
    /// });
    ///
    /// if (composite.IsBroken())
    /// {
    ///     IReadOnlyCollection&lt;BusinessRuleResult&gt; allBroken = composite.EvaluateAll();
    /// }
    /// </code>
    /// </example>
    public class CompositeBusinessRule : IComposedBusinessRule, IExplainableBusinessRule
    {
        private const string CompositeCode = "CompositeRule";
        private const string CompositeMessage = "One or more business rules failed.";
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();
        private static readonly IReadOnlyList<BusinessRuleResult> NoResults = Array.Empty<BusinessRuleResult>();
        private readonly List<IBusinessRule> _rules = new List<IBusinessRule>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeBusinessRule"/> class.
        /// </summary>
        /// <param name="rules">The collection of rules to include in this composite.</param>
        public CompositeBusinessRule(IEnumerable<IBusinessRule> rules) => _rules.AddRange(rules);

        /// <inheritdoc />
        public string Code => CompositeCode;

        /// <inheritdoc />
        public string Message => CompositeMessage;

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <inheritdoc />
        public string Name => "CompositeBusinessRule";

        /// <inheritdoc />
        public string Description => "A composite rule that evaluates multiple business rules.";

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the underlying rules in this composite.
        /// </summary>
        public IReadOnlyList<IBusinessRule> Rules => _rules.AsReadOnly();

        /// <inheritdoc />
        public RuleOperator Operator => RuleOperator.All;

        /// <inheritdoc />
        public IReadOnlyList<IBusinessRule> Operands => Rules;

        /// <inheritdoc />
        public bool IsBroken() => EvaluateCore(null, null) != null;

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context) => EvaluateCore(context, null) != null;

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when any child rules are broken; carries each broken child result individually.</exception>
        public void Check()
        {
            ThrowIfBroken(EvaluateCore(null, null));
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when any child rules are broken; carries each broken child result individually.</exception>
        public void Check(BusinessRuleContext context)
        {
            ThrowIfBroken(EvaluateCore(context, null));
        }

        /// <inheritdoc />
        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);

        /// <summary>
        /// Evaluates all child rules and returns results for all broken rules.
        /// </summary>
        /// <returns>A collection of results for all broken child rules.</returns>
        public IReadOnlyCollection<BusinessRuleResult> EvaluateAll()
            => EvaluateCore(null, null)?.Details ?? NoResults;

        /// <summary>
        /// Evaluates all child rules using context data and returns results for all broken rules.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <returns>A collection of results for all broken child rules.</returns>
        public IReadOnlyCollection<BusinessRuleResult> EvaluateAll(BusinessRuleContext context)
            => EvaluateCore(context, null)?.Details ?? NoResults;

        BusinessRuleResult? IExplainableBusinessRule.EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
            => EvaluateCore(context, trace);

        private BusinessRuleResult? EvaluateCore(BusinessRuleContext? context, RuleExplanationTrace? trace)
        {
            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            List<BusinessRuleResult>? details = null;
            foreach (IBusinessRule rule in _rules)
            {
                BusinessRuleResult? result = BusinessRuleWalker.EvaluateNode(rule, context, children);
                if (result != null)
                {
                    details = details ?? new List<BusinessRuleResult>();
                    details.Add(result);
                }
            }

            bool isBroken = details != null;

            if (trace != null && children != null)
            {
                trace.Add(BusinessRuleWalker.Evaluated(this, isBroken, RuleExplanationText.AllOperator, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken ? new BusinessRuleResult(Code, Message, Severity, details ?? NoResults) : null;
        }

        private static void ThrowIfBroken(BusinessRuleResult? result)
        {
            if (result != null)
            {
                throw new BusinessRuleValidationException(result.Details);
            }
        }
    }
}
