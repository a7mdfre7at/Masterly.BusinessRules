using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    public static class AsyncBusinessRuleChecker
    {
        public static async Task CheckAllAsync(BusinessRuleContext context, params IAsyncBusinessRule[] rules)
        {
            var brokenRules = new List<BusinessRuleResult>();

            foreach (IAsyncBusinessRule rule in rules)
            {
                BusinessRuleResult? result = await rule.EvaluateAsync(context);
                if (result != null)
                    brokenRules.Add(result);
            }

            if (brokenRules.Any())
            {
                throw new BusinessRuleValidationException(brokenRules);
            }
        }
    }
}
