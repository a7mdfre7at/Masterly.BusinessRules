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
        /// <summary>
        /// Gets the collection of broken rule results.
        /// </summary>
        public IReadOnlyCollection<BusinessRuleResult> BrokenRules { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleValidationException"/> class.
        /// </summary>
        /// <param name="brokenRules">The collection of broken rule results.</param>
        public BusinessRuleValidationException(IEnumerable<BusinessRuleResult> brokenRules)
            : base("One or more business rules were violated.")
        {
            BrokenRules = brokenRules.ToList().AsReadOnly();
        }
    }
}