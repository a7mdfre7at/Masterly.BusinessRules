using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Builds the broken result that stands in for a faulted rule under <see cref="FaultPolicy.TreatAsBroken"/>.
    /// The rule's metadata getters are read inside a secondary guard, so a <c>Code</c> or <c>Message</c> getter that throws cannot
    /// escape the checker and mask the original fault.
    /// </summary>
    internal static class RuleFaultResult
    {
        private const string FaultedMessagePrefix = "Rule evaluation faulted";
        private static readonly IReadOnlyList<string> NoTags = Array.Empty<string>();

        /// <summary>
        /// Creates the stand-in result for a faulted synchronous rule.
        /// </summary>
        public static BusinessRuleResult Create(IBusinessRule rule, Exception fault)
        {
            try
            {
                return RuleResultFactory.Create(rule);
            }
            catch (Exception)
            {
                return Fallback(rule.GetType(), fault);
            }
        }

        /// <summary>
        /// Creates the stand-in result for a faulted asynchronous rule.
        /// </summary>
        public static BusinessRuleResult Create(IAsyncBusinessRule rule, Exception fault)
        {
            try
            {
                return RuleResultFactory.Create(rule);
            }
            catch (Exception)
            {
                return Fallback(rule.GetType(), fault);
            }
        }

        private static BusinessRuleResult Fallback(Type ruleType, Exception fault)
        {
            return new BusinessRuleResult(
                ruleType.Name,
                $"{FaultedMessagePrefix}: {RuleExplanationText.FaultReason(fault)}",
                RuleSeverity.Error,
                ruleType.Name,
                string.Empty,
                NoTags);
        }
    }
}
