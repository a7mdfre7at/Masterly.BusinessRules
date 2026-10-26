using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Splits broken results around a severity threshold. Shared by both result types, both checkers and the
    /// <c>Check(context, throwOn)</c> extensions so "at or above" means the same thing everywhere.
    /// </summary>
    internal static class RuleSeverityThreshold
    {
        /// <summary>
        /// Returns true when <paramref name="severity"/> is at or above <paramref name="threshold"/>.
        /// </summary>
        public static bool IsAtLeast(RuleSeverity severity, RuleSeverity threshold)
        {
            return severity >= threshold;
        }

        /// <summary>
        /// Partitions <paramref name="brokenRules"/> into the results at or above the threshold (to be thrown)
        /// and the results below it (to be collected), preserving order.
        /// </summary>
        public static void Split(
            IEnumerable<BusinessRuleResult> brokenRules,
            RuleSeverity threshold,
            out List<BusinessRuleResult> thrown,
            out List<BusinessRuleResult> collected)
        {
            thrown = new List<BusinessRuleResult>();
            collected = new List<BusinessRuleResult>();

            foreach (BusinessRuleResult result in brokenRules)
            {
                if (IsAtLeast(result.Severity, threshold))
                {
                    thrown.Add(result);
                }
                else
                {
                    collected.Add(result);
                }
            }
        }
    }
}
