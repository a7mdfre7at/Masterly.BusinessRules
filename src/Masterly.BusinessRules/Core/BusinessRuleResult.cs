using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Represents the result of a broken business rule evaluation.
    /// This immutable class contains the code, message, and severity of the rule violation, plus the
    /// <see cref="Details"/> of the member rules that broke a composed rule (group, composite, And/Or).
    /// </summary>
    public sealed class BusinessRuleResult
    {
        private static readonly IReadOnlyList<BusinessRuleResult> NoDetails = Array.Empty<BusinessRuleResult>();

        /// <summary>
        /// Gets the unique code identifying the broken rule.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the human-readable message describing the rule violation.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the severity level of the rule violation.
        /// </summary>
        public RuleSeverity Severity { get; }

        /// <summary>
        /// Gets the results of the member rules that caused a composed rule to break, in evaluation order.
        /// Empty for a leaf rule. Use <see cref="Flatten"/> or <see cref="BusinessRuleValidationException.Flatten"/>
        /// to reach the leaf results of a nested tree.
        /// </summary>
        public IReadOnlyList<BusinessRuleResult> Details { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleResult"/> class with no details.
        /// </summary>
        /// <param name="code">The unique code identifying the broken rule.</param>
        /// <param name="message">The human-readable message describing the rule violation.</param>
        /// <param name="severity">The severity level of the rule violation.</param>
        public BusinessRuleResult(string code, string message, RuleSeverity severity)
            : this(code, message, severity, NoDetails)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleResult"/> class carrying member results.
        /// </summary>
        /// <param name="code">The unique code identifying the broken rule.</param>
        /// <param name="message">The human-readable message describing the rule violation.</param>
        /// <param name="severity">The severity level of the rule violation.</param>
        /// <param name="details">The results of the member rules that caused this rule to break; copied, so later changes to the source are not reflected.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="details"/> is null.</exception>
        public BusinessRuleResult(string code, string message, RuleSeverity severity, IEnumerable<BusinessRuleResult> details)
        {
            if (details == null)
            {
                throw new ArgumentNullException(nameof(details));
            }

            Code = code;
            Message = message;
            Severity = severity;
            Details = ReferenceEquals(details, NoDetails) ? NoDetails : details.ToArray();
        }

        /// <summary>
        /// Returns the leaf results of this result: the result itself when it has no <see cref="Details"/>,
        /// otherwise the leaves of every detail, recursively, in evaluation order.
        /// </summary>
        /// <returns>The leaf results.</returns>
        public IEnumerable<BusinessRuleResult> Flatten()
        {
            if (Details.Count == 0)
            {
                yield return this;
                yield break;
            }

            foreach (BusinessRuleResult detail in Details)
            {
                foreach (BusinessRuleResult leaf in detail.Flatten())
                {
                    yield return leaf;
                }
            }
        }
    }
}
