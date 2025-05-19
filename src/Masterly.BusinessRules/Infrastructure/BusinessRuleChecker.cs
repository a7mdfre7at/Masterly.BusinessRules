using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    public static class BusinessRuleChecker
    {
        public static void CheckAll(params BaseBusinessRule[] rules)
        {
            List<BusinessRuleResult> brokenRules = rules.Where(rule => rule.IsBroken()).Select(rule => rule.Evaluate()).ToList();

            if (brokenRules.Any())
            {
                throw new BusinessRuleValidationException(brokenRules);
            }
        }
    }
}