using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    public sealed class BusinessRuleValidationException : Exception
    {
        public IReadOnlyCollection<BusinessRuleResult> BrokenRules { get; }

        public BusinessRuleValidationException(IEnumerable<BusinessRuleResult> brokenRules)
            : base("One or more business rules were violated.")
        {
            BrokenRules = brokenRules.ToList().AsReadOnly();
        }
    }
}