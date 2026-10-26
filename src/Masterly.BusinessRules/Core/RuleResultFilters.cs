using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Shared tag and category filters over broken results, used by <see cref="BusinessRuleValidationException"/>,
    /// <see cref="RuleEvaluationResult"/> and <see cref="AsyncRuleEvaluationResult"/>. Tags compare case-insensitively
    /// (as <c>BusinessRuleChecker.CheckByTags</c> does); categories compare ordinally (as <c>CheckByCategory</c> does).
    /// </summary>
    internal static class RuleResultFilters
    {
        /// <summary>
        /// Returns the results whose rule carries the tag.
        /// </summary>
        public static IEnumerable<BusinessRuleResult> ByTag(IEnumerable<BusinessRuleResult> results, string tag)
        {
            if (tag == null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            return results.Where(result => result.HasTag(tag));
        }

        /// <summary>
        /// Returns the results whose rule belongs to the category.
        /// </summary>
        public static IEnumerable<BusinessRuleResult> ByCategory(IEnumerable<BusinessRuleResult> results, string category)
        {
            if (category == null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            return results.Where(result => string.Equals(result.Category, category, StringComparison.Ordinal));
        }
    }
}
