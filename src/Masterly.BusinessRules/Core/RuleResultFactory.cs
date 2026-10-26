using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Builds <see cref="BusinessRuleResult"/> instances from a rule's <see cref="IBusinessRuleMetadata"/> so that
    /// every in-library construction site populates <see cref="BusinessRuleResult.Name"/>,
    /// <see cref="BusinessRuleResult.Category"/> and <see cref="BusinessRuleResult.Tags"/> the same way, and guards the
    /// <c>Describe</c> hook so that an exception inside it degrades to <see cref="RuleResultDetails.Empty"/> instead of
    /// masking the violation.
    /// A rule whose <c>Name</c>, <c>Category</c> or <c>Tags</c> getter returns null (a contract violation that older,
    /// nullable-oblivious rules can commit) is reported with the code as <c>Name</c>, an empty category and no tags
    /// instead of failing result construction.
    /// </summary>
    internal static class RuleResultFactory
    {
        private static readonly IReadOnlyList<BusinessRuleResult> NoMembers = Array.Empty<BusinessRuleResult>();
        private static readonly IReadOnlyList<string> NoTags = Array.Empty<string>();

        /// <summary>
        /// Creates a result carrying only the metadata of a rule.
        /// </summary>
        public static BusinessRuleResult Create(IBusinessRuleMetadata rule)
        {
            return Create(rule, RuleResultDetails.Empty, NoMembers, null);
        }

        /// <summary>
        /// Creates a result for a composed rule carrying its broken member results.
        /// </summary>
        public static BusinessRuleResult Create(IBusinessRuleMetadata rule, IEnumerable<BusinessRuleResult> memberDetails)
        {
            return Create(rule, RuleResultDetails.Empty, memberDetails, null);
        }

        /// <summary>
        /// Creates a result for a leaf rule carrying the details produced by its <c>Describe</c> hook.
        /// </summary>
        public static BusinessRuleResult Create(IBusinessRuleMetadata rule, RuleResultDetails details, IFormatProvider? formatProvider)
        {
            return Create(rule, details, NoMembers, formatProvider);
        }

        /// <summary>
        /// Runs a <c>Describe</c> hook on the broken path. Any exception other than cancellation degrades to
        /// <see cref="RuleResultDetails.Empty"/>, so a faulty description can never hide the violation it describes.
        /// </summary>
        public static RuleResultDetails DescribeGuarded(Func<RuleResultDetails> describe)
        {
            try
            {
                return describe() ?? RuleResultDetails.Empty;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return RuleResultDetails.Empty;
            }
        }

        private static BusinessRuleResult Create(IBusinessRuleMetadata rule, RuleResultDetails details, IEnumerable<BusinessRuleResult> memberDetails, IFormatProvider? formatProvider)
        {
            string code = rule.Code;
            return new BusinessRuleResult(
                code,
                rule.Message,
                rule.Severity,
                rule.Name ?? code,
                rule.Category ?? string.Empty,
                rule.Tags ?? NoTags,
                details,
                memberDetails,
                formatProvider);
        }
    }
}
