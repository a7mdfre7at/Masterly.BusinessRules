using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Masterly.BusinessRules.Abstractions;
using Masterly.BusinessRules.Core;

namespace Masterly.BusinessRules.Infrastructure
{
    public static class BusinessRuleChecker
    {
        public static void CheckAll(params BaseBusinessRule[] rules)
        {
            var brokenRules = new List<BusinessRuleResult>();

            foreach (BaseBusinessRule rule in rules)
            {
                if (rule.IsBroken())
                {
                    brokenRules.Add(rule.Evaluate());
                }
            }

            if (brokenRules.Any())
            {
                throw new BusinessRuleValidationException(brokenRules);
            }
        }
    }
}