using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Factory for the N-ary synchronous quantifiers <see cref="GroupBusinessRule"/>.
    /// Unlike the legacy <c>And</c>/<c>Or</c> operators, the groups are stated in terms of how many members must <b>pass</b>:
    /// <see cref="All(string, string, IBusinessRule[])"/> is broken when any member is broken,
    /// <see cref="Any(string, string, IBusinessRule[])"/> is broken only when every member is broken.
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule payment = RuleGroup.Any("PAYMENT.NONE_VALID", "At least one payment method must be valid", card, bank, credit);
    /// IBusinessRule kyc     = RuleGroup.All("KYC.INCOMPLETE", "All KYC checks must pass", idRule, addressRule).WithShortCircuit();
    /// IBusinessRule tier    = RuleGroup.AtLeast(2, "TIER.INSUFFICIENT", "Need two of three signals", r1, r2, r3);
    /// </code>
    /// </example>
    public static class RuleGroup
    {
        private const int NoThreshold = 0;

        /// <summary>
        /// Creates a group that is broken when <b>any</b> member is broken (every member must pass).
        /// </summary>
        /// <param name="code">The code reported when the group is broken.</param>
        /// <param name="message">The message reported when the group is broken.</param>
        /// <param name="members">The member rules; at least one is required.</param>
        /// <returns>A new group that evaluates every member unless <see cref="GroupBusinessRule.WithShortCircuit"/> is applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="members"/> is empty or contains null.</exception>
        public static GroupBusinessRule All(string code, string message, params IBusinessRule[] members)
            => Create(RuleGroupQuantifier.All, NoThreshold, code, message, members);

        /// <summary>
        /// Creates a group that is broken when <b>any</b> member is broken (every member must pass).
        /// </summary>
        /// <param name="code">The code reported when the group is broken.</param>
        /// <param name="message">The message reported when the group is broken.</param>
        /// <param name="members">The member rules; at least one is required.</param>
        /// <returns>A new group that evaluates every member unless <see cref="GroupBusinessRule.WithShortCircuit"/> is applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="members"/> is empty or contains null.</exception>
        public static GroupBusinessRule All(string code, string message, IEnumerable<IBusinessRule> members)
            => Create(RuleGroupQuantifier.All, NoThreshold, code, message, members);

        /// <summary>
        /// Creates a group that is broken only when <b>every</b> member is broken (at least one member must pass).
        /// </summary>
        /// <param name="code">The code reported when the group is broken.</param>
        /// <param name="message">The message reported when the group is broken.</param>
        /// <param name="members">The member rules; at least one is required.</param>
        /// <returns>A new group that evaluates every member unless <see cref="GroupBusinessRule.WithShortCircuit"/> is applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="members"/> is empty or contains null.</exception>
        public static GroupBusinessRule Any(string code, string message, params IBusinessRule[] members)
            => Create(RuleGroupQuantifier.Any, NoThreshold, code, message, members);

        /// <summary>
        /// Creates a group that is broken only when <b>every</b> member is broken (at least one member must pass).
        /// </summary>
        /// <param name="code">The code reported when the group is broken.</param>
        /// <param name="message">The message reported when the group is broken.</param>
        /// <param name="members">The member rules; at least one is required.</param>
        /// <returns>A new group that evaluates every member unless <see cref="GroupBusinessRule.WithShortCircuit"/> is applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="members"/> is empty or contains null.</exception>
        public static GroupBusinessRule Any(string code, string message, IEnumerable<IBusinessRule> members)
            => Create(RuleGroupQuantifier.Any, NoThreshold, code, message, members);

        /// <summary>
        /// Creates a group that is broken when fewer than <paramref name="count"/> members pass.
        /// </summary>
        /// <param name="count">The minimum number of members that must pass; between 0 and the number of members.</param>
        /// <param name="code">The code reported when the group is broken.</param>
        /// <param name="message">The message reported when the group is broken.</param>
        /// <param name="members">The member rules; at least one is required.</param>
        /// <returns>A new group that evaluates every member unless <see cref="GroupBusinessRule.WithShortCircuit"/> is applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="members"/> is empty or contains null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative or exceeds the member count.</exception>
        public static GroupBusinessRule AtLeast(int count, string code, string message, params IBusinessRule[] members)
            => Create(RuleGroupQuantifier.AtLeast, count, code, message, members);

        /// <summary>
        /// Creates a group that is broken when fewer than <paramref name="count"/> members pass.
        /// </summary>
        /// <param name="count">The minimum number of members that must pass; between 0 and the number of members.</param>
        /// <param name="code">The code reported when the group is broken.</param>
        /// <param name="message">The message reported when the group is broken.</param>
        /// <param name="members">The member rules; at least one is required.</param>
        /// <returns>A new group that evaluates every member unless <see cref="GroupBusinessRule.WithShortCircuit"/> is applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="members"/> is empty or contains null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative or exceeds the member count.</exception>
        public static GroupBusinessRule AtLeast(int count, string code, string message, IEnumerable<IBusinessRule> members)
            => Create(RuleGroupQuantifier.AtLeast, count, code, message, members);

        /// <summary>
        /// Creates a group that is broken unless exactly <paramref name="count"/> members pass.
        /// </summary>
        /// <param name="count">The exact number of members that must pass; between 0 and the number of members.</param>
        /// <param name="code">The code reported when the group is broken.</param>
        /// <param name="message">The message reported when the group is broken.</param>
        /// <param name="members">The member rules; at least one is required.</param>
        /// <returns>A new group that evaluates every member unless <see cref="GroupBusinessRule.WithShortCircuit"/> is applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="members"/> is empty or contains null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative or exceeds the member count.</exception>
        public static GroupBusinessRule Exactly(int count, string code, string message, params IBusinessRule[] members)
            => Create(RuleGroupQuantifier.Exactly, count, code, message, members);

        /// <summary>
        /// Creates a group that is broken unless exactly <paramref name="count"/> members pass.
        /// </summary>
        /// <param name="count">The exact number of members that must pass; between 0 and the number of members.</param>
        /// <param name="code">The code reported when the group is broken.</param>
        /// <param name="message">The message reported when the group is broken.</param>
        /// <param name="members">The member rules; at least one is required.</param>
        /// <returns>A new group that evaluates every member unless <see cref="GroupBusinessRule.WithShortCircuit"/> is applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="members"/> is empty or contains null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative or exceeds the member count.</exception>
        public static GroupBusinessRule Exactly(int count, string code, string message, IEnumerable<IBusinessRule> members)
            => Create(RuleGroupQuantifier.Exactly, count, code, message, members);

        private static GroupBusinessRule Create(RuleGroupQuantifier quantifier, int requestedThreshold, string code, string message, IEnumerable<IBusinessRule> members)
        {
            if (code == null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            IReadOnlyList<IBusinessRule> validatedMembers = RuleGroupQuantifierLogic.ValidateMembers(members, nameof(members));
            int threshold = RuleGroupQuantifierLogic.ResolveThreshold(quantifier, requestedThreshold, validatedMembers.Count);
            RuleGroupQuantifierLogic.ValidateThreshold(threshold, validatedMembers.Count, "count");

            return new GroupBusinessRule(quantifier, threshold, code, message, RuleSeverity.Error, validatedMembers, shortCircuit: false);
        }
    }
}
