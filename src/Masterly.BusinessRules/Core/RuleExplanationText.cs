using System;
using System.Globalization;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// String constants shared by the explanation walk: operator labels, outcome labels and standard reasons.
    /// </summary>
    internal static class RuleExplanationText
    {
        public const string LeafOperator = "";
        public const string AndOperator = "AND";
        public const string OrOperator = "OR";
        public const string NotOperator = "NOT";
        public const string AllOperator = "ALL";
        public const string AnyOperator = "ANY";
        public const string AtLeastOperator = "AT LEAST";
        public const string ExactlyOperator = "EXACTLY";
        public const string CachedOperator = "CACHED";
        public const string WhenOperator = "WHEN";
        public const string AdapterOperator = "ADAPTER";
        public const string SeverityOperator = "SEVERITY";

        public const string NoReason = "";
        public const string ShortCircuitReason = "short-circuit";
        public const string PreconditionNotMetReason = "precondition not met";
        public const string CacheHitReason = "cache hit";

        public const string PassedLabel = "PASSED";
        public const string BrokenLabel = "BROKEN";
        public const string SkippedLabel = "SKIPPED";
        public const string CacheHitLabel = "CACHE HIT";
        public const string NotEvaluatedLabel = "NOT EVALUATED";
        public const string FaultedLabel = "FAULTED";

        /// <summary>
        /// Returns the label printed for an operator, including the threshold for the counting quantifiers.
        /// </summary>
        public static string OperatorLabel(RuleOperator ruleOperator, int threshold)
        {
            switch (ruleOperator)
            {
                case RuleOperator.And:
                    return AndOperator;
                case RuleOperator.Or:
                    return OrOperator;
                case RuleOperator.Not:
                    return NotOperator;
                case RuleOperator.All:
                    return AllOperator;
                case RuleOperator.Any:
                    return AnyOperator;
                case RuleOperator.AtLeast:
                    return $"{AtLeastOperator} {threshold.ToString(CultureInfo.InvariantCulture)}";
                case RuleOperator.Exactly:
                    return $"{ExactlyOperator} {threshold.ToString(CultureInfo.InvariantCulture)}";
                default:
                    return ruleOperator.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// Returns the label printed for an outcome.
        /// </summary>
        public static string OutcomeLabel(RuleOutcome outcome)
        {
            switch (outcome)
            {
                case RuleOutcome.Passed:
                    return PassedLabel;
                case RuleOutcome.Broken:
                    return BrokenLabel;
                case RuleOutcome.Skipped:
                    return SkippedLabel;
                case RuleOutcome.CacheHit:
                    return CacheHitLabel;
                case RuleOutcome.NotEvaluated:
                    return NotEvaluatedLabel;
                case RuleOutcome.Faulted:
                    return FaultedLabel;
                default:
                    return outcome.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// Builds the reason recorded on a faulted node.
        /// </summary>
        public static string FaultReason(Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }
}
