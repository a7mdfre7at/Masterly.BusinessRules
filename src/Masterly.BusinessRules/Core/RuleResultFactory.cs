using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Builds <see cref="BusinessRuleResult"/> instances from a rule's metadata so that every in-library construction
    /// site populates <see cref="BusinessRuleResult.Name"/>, <see cref="BusinessRuleResult.Category"/> and
    /// <see cref="BusinessRuleResult.Tags"/> the same way, and guards the <c>Describe</c> hook so that an exception
    /// inside it degrades to <see cref="RuleResultDetails.Empty"/> instead of masking the violation.
    /// </summary>
    internal static class RuleResultFactory
    {
        private static readonly IReadOnlyList<BusinessRuleResult> NoMembers = Array.Empty<BusinessRuleResult>();

        /// <summary>
        /// Creates a result carrying only the metadata of a synchronous rule.
        /// </summary>
        public static BusinessRuleResult Create(IBusinessRule rule)
        {
            return Create(rule, RuleResultDetails.Empty, NoMembers, null);
        }

        /// <summary>
        /// Creates a result carrying only the metadata of an asynchronous rule.
        /// </summary>
        public static BusinessRuleResult Create(IAsyncBusinessRule rule)
        {
            return Create(rule, RuleResultDetails.Empty, NoMembers, null);
        }

        /// <summary>
        /// Creates a result for a composed synchronous rule carrying its broken member results.
        /// </summary>
        public static BusinessRuleResult Create(IBusinessRule rule, IEnumerable<BusinessRuleResult> memberDetails)
        {
            return Create(rule, RuleResultDetails.Empty, memberDetails, null);
        }

        /// <summary>
        /// Creates a result for a composed asynchronous rule carrying its broken member results.
        /// </summary>
        public static BusinessRuleResult Create(IAsyncBusinessRule rule, IEnumerable<BusinessRuleResult> memberDetails)
        {
            return Create(rule, RuleResultDetails.Empty, memberDetails, null);
        }

        /// <summary>
        /// Creates a result for a synchronous leaf rule carrying the details produced by its <c>Describe</c> hook.
        /// </summary>
        public static BusinessRuleResult Create(IBusinessRule rule, RuleResultDetails details, IFormatProvider? formatProvider)
        {
            return Create(rule, details, NoMembers, formatProvider);
        }

        /// <summary>
        /// Creates a result for an asynchronous leaf rule carrying the details produced by its <c>Describe</c> hook.
        /// </summary>
        public static BusinessRuleResult Create(IAsyncBusinessRule rule, RuleResultDetails details, IFormatProvider? formatProvider)
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

        private static BusinessRuleResult Create(IBusinessRule rule, RuleResultDetails details, IEnumerable<BusinessRuleResult> memberDetails, IFormatProvider? formatProvider)
        {
            return new BusinessRuleResult(rule.Code, rule.Message, rule.Severity, rule.Name, rule.Category, rule.Tags, details, memberDetails, formatProvider);
        }

        private static BusinessRuleResult Create(IAsyncBusinessRule rule, RuleResultDetails details, IEnumerable<BusinessRuleResult> memberDetails, IFormatProvider? formatProvider)
        {
            return new BusinessRuleResult(rule.Code, rule.Message, rule.Severity, rule.Name, rule.Category, rule.Tags, details, memberDetails, formatProvider);
        }
    }
}
