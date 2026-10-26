using System;
using System.Collections.Generic;
using System.Globalization;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Counting logic shared by <see cref="GroupBusinessRule"/> and <see cref="GroupAsyncBusinessRule"/>.
    /// A group counts passing members; <see cref="RuleGroupQuantifier.All"/> and <see cref="RuleGroupQuantifier.Any"/>
    /// are the <c>AtLeast</c> cases with thresholds equal to the member count and one respectively.
    /// </summary>
    internal static class RuleGroupQuantifierLogic
    {
        private const int AnyThreshold = 1;
        private const string MembersRequiredMessage = "A rule group requires at least one member rule.";
        private const string NullMemberMessage = "A rule group cannot contain a null member rule.";
        private const string ThresholdRangeMessage = "The threshold must be between 0 and the number of member rules.";

        /// <summary>
        /// Resolves the number of members that must pass for the given quantifier.
        /// </summary>
        public static int ResolveThreshold(RuleGroupQuantifier quantifier, int requestedThreshold, int memberCount)
        {
            switch (quantifier)
            {
                case RuleGroupQuantifier.All:
                    return memberCount;
                case RuleGroupQuantifier.Any:
                    return AnyThreshold;
                default:
                    return requestedThreshold;
            }
        }

        /// <summary>
        /// Maps a quantifier to its <see cref="RuleOperator"/>.
        /// </summary>
        public static RuleOperator ToOperator(RuleGroupQuantifier quantifier)
        {
            switch (quantifier)
            {
                case RuleGroupQuantifier.All:
                    return RuleOperator.All;
                case RuleGroupQuantifier.Any:
                    return RuleOperator.Any;
                case RuleGroupQuantifier.AtLeast:
                    return RuleOperator.AtLeast;
                default:
                    return RuleOperator.Exactly;
            }
        }

        /// <summary>
        /// Returns whether a group is broken given how many members passed.
        /// </summary>
        public static bool IsBroken(RuleGroupQuantifier quantifier, int threshold, int passedCount)
        {
            return quantifier == RuleGroupQuantifier.Exactly ? passedCount != threshold : passedCount < threshold;
        }

        /// <summary>
        /// Returns whether the verdict is already decided by the members evaluated so far, so a short-circuiting
        /// group can stop evaluating.
        /// </summary>
        public static bool IsDecided(RuleGroupQuantifier quantifier, int threshold, int passedCount, int brokenCount, int memberCount)
        {
            int remaining = memberCount - passedCount - brokenCount;
            if (remaining == 0)
            {
                return true;
            }

            bool cannotReachThreshold = passedCount + remaining < threshold;
            if (quantifier == RuleGroupQuantifier.Exactly)
            {
                return cannotReachThreshold || passedCount > threshold;
            }

            return cannotReachThreshold || passedCount >= threshold;
        }

        /// <summary>
        /// Builds the default description of a group.
        /// </summary>
        public static string Describe(RuleGroupQuantifier quantifier, int threshold, int memberCount)
        {
            string label = RuleExplanationText.OperatorLabel(ToOperator(quantifier), threshold);
            return $"{label} of {memberCount.ToString(CultureInfo.InvariantCulture)} member rules must pass.";
        }

        /// <summary>
        /// Copies the members into a read-only list, rejecting null or empty collections and null members.
        /// </summary>
        public static IReadOnlyList<TRule> ValidateMembers<TRule>(IEnumerable<TRule>? members, string parameterName) where TRule : class
        {
            if (members == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<TRule> copy = new List<TRule>(members);
            if (copy.Count == 0)
            {
                throw new ArgumentException(MembersRequiredMessage, parameterName);
            }

            foreach (TRule member in copy)
            {
                if (member == null)
                {
                    throw new ArgumentException(NullMemberMessage, parameterName);
                }
            }

            return copy.AsReadOnly();
        }

        /// <summary>
        /// Rejects a threshold outside <c>0..memberCount</c>.
        /// </summary>
        public static void ValidateThreshold(int threshold, int memberCount, string parameterName)
        {
            if (threshold < 0 || threshold > memberCount)
            {
                throw new ArgumentOutOfRangeException(parameterName, threshold, ThresholdRangeMessage);
            }
        }
    }
}
