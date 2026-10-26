using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Exception thrown when one or more business rules are violated.
    /// Contains a collection of all broken rule results.
    /// </summary>
    public sealed class BusinessRuleValidationException : Exception
    {
        private const string DefaultMessage = "One or more business rules were violated.";

        /// <summary>
        /// Gets the collection of broken rule results. A result produced by a composed rule carries its broken
        /// members in <see cref="BusinessRuleResult.Details"/>; use <see cref="Flatten"/> to reach the leaves.
        /// </summary>
        public IReadOnlyCollection<BusinessRuleResult> BrokenRules { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleValidationException"/> class.
        /// </summary>
        /// <param name="brokenRules">The collection of broken rule results.</param>
        public BusinessRuleValidationException(IEnumerable<BusinessRuleResult> brokenRules)
            : base(DefaultMessage)
        {
            BrokenRules = brokenRules.ToList().AsReadOnly();
        }

        /// <summary>
        /// Returns the leaf results of every broken rule: results without <see cref="BusinessRuleResult.Details"/>
        /// are returned as they are, composed results are replaced by their member leaves, recursively.
        /// </summary>
        /// <returns>The leaf results in evaluation order.</returns>
        public IEnumerable<BusinessRuleResult> Flatten()
        {
            return BrokenRules.SelectMany(result => result.Flatten());
        }
    }
}
