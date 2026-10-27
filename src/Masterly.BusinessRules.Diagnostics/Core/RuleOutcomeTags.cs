namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The low-cardinality values written to the <c>business_rule.outcome</c> span attribute, the <c>outcome</c> metric
    /// tag and the <c>Outcome</c> log field.
    /// </summary>
    public static class RuleOutcomeTags
    {
        /// <summary>
        /// The rule was evaluated and is not broken.
        /// </summary>
        public const string Passed = "passed";

        /// <summary>
        /// The rule was evaluated and is broken.
        /// </summary>
        public const string Broken = "broken";

        /// <summary>
        /// The rule threw during evaluation.
        /// </summary>
        public const string Faulted = "faulted";

        /// <summary>
        /// A shadow-mode gated rule observed a violation it did not report as broken. Recorded in addition to the
        /// gated rule's own <see cref="Passed"/> measurement.
        /// </summary>
        public const string Shadow = "shadow";

        /// <summary>
        /// The evaluation was cancelled before it produced a verdict. Written by the <c>Traced()</c> decorators and by the
        /// tracing observers (rule and batch spans) when a checker batch is cancelled; never counted on the metrics.
        /// </summary>
        public const string Cancelled = "cancelled";

        /// <summary>
        /// Returns <see cref="Broken"/> or <see cref="Passed"/> for an evaluated rule.
        /// </summary>
        /// <param name="isBroken">True when the rule is broken.</param>
        /// <returns>The outcome tag value.</returns>
        public static string ForVerdict(bool isBroken)
        {
            return isBroken ? Broken : Passed;
        }

        /// <summary>
        /// Returns the outcome tag value of a whole batch: <see cref="Faulted"/> when any rule threw (an operational
        /// problem outranks a business verdict), otherwise <see cref="Broken"/> when any rule is broken, otherwise
        /// <see cref="Passed"/>.
        /// </summary>
        /// <param name="hasFaults">True when at least one rule threw.</param>
        /// <param name="hasBrokenRules">True when at least one rule is broken.</param>
        /// <returns>The outcome tag value.</returns>
        public static string ForBatch(bool hasFaults, bool hasBrokenRules)
        {
            if (hasFaults)
            {
                return Faulted;
            }

            return hasBrokenRules ? Broken : Passed;
        }
    }
}
