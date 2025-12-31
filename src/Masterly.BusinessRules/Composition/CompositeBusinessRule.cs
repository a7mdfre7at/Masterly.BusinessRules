using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A composite business rule that contains and evaluates multiple child rules.
    /// The composite is considered broken if any of its child rules are broken.
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
    public class CompositeBusinessRule : IBusinessRule
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();
        private readonly List<IBusinessRule> _rules = new List<IBusinessRule>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeBusinessRule"/> class.
        /// </summary>
        /// <param name="rules">The collection of rules to include in this composite.</param>
        public CompositeBusinessRule(IEnumerable<IBusinessRule> rules) => _rules.AddRange(rules);

        /// <inheritdoc />
        public string Code => "CompositeRule";

        /// <inheritdoc />
        public string Message => "One or more business rules failed.";

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
        public bool IsBroken() => _rules.Any(r => r.IsBroken());

        /// <inheritdoc />
        public bool IsBroken(BusinessRuleContext context) => _rules.Any(r => r.IsBroken(context));

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when any child rules are broken.</exception>
        public void Check()
        {
            List<BusinessRuleResult> broken = _rules.Where(r => r.IsBroken()).Select(r => r.Evaluate()).ToList();

            if (broken.Any())
                throw new BusinessRuleValidationException(broken);
        }

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when any child rules are broken.</exception>
        public void Check(BusinessRuleContext context)
        {
            List<BusinessRuleResult> broken = _rules.Where(r => r.IsBroken(context)).Select(r => r.Evaluate()).ToList();

            if (broken.Any())
                throw new BusinessRuleValidationException(broken);
        }

        /// <inheritdoc />
        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);

        /// <summary>
        /// Evaluates all child rules and returns results for all broken rules.
        /// </summary>
        /// <returns>A collection of results for all broken child rules.</returns>
        public IReadOnlyCollection<BusinessRuleResult> EvaluateAll()
            => _rules.Where(r => r.IsBroken()).Select(r => r.Evaluate()).ToList().AsReadOnly();

        /// <summary>
        /// Evaluates all child rules using context data and returns results for all broken rules.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <returns>A collection of results for all broken child rules.</returns>
        public IReadOnlyCollection<BusinessRuleResult> EvaluateAll(BusinessRuleContext context)
            => _rules.Where(r => r.IsBroken(context)).Select(r => r.Evaluate()).ToList().AsReadOnly();
    }
}
