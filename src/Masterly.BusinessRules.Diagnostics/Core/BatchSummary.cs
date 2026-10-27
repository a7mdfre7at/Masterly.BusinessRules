using System;
using System.Linq;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The counts a completed batch is reported with, read once from a <see cref="RuleEvaluationResult"/> or an
    /// <see cref="AsyncRuleEvaluationResult"/> so the span, metric and log writers share one view of the batch.
    /// </summary>
    internal readonly struct BatchSummary
    {
        private BatchSummary(int ruleCount, int brokenCount, int faultedCount, int passedCount, TimeSpan totalDuration)
        {
            RuleCount = ruleCount;
            BrokenCount = brokenCount;
            FaultedCount = faultedCount;
            PassedCount = passedCount;
            TotalDuration = totalDuration;
        }

        /// <summary>
        /// Gets the number of ledger entries (every submitted rule, evaluated or not).
        /// </summary>
        public int RuleCount { get; }

        /// <summary>
        /// Gets the number of broken results.
        /// </summary>
        public int BrokenCount { get; }

        /// <summary>
        /// Gets the number of rules that threw.
        /// </summary>
        public int FaultedCount { get; }

        /// <summary>
        /// Gets the number of rules that passed.
        /// </summary>
        public int PassedCount { get; }

        /// <summary>
        /// Gets the wall-clock duration of the batch.
        /// </summary>
        public TimeSpan TotalDuration { get; }

        /// <summary>
        /// Gets the batch outcome tag (see <see cref="RuleOutcomeTags.ForBatch"/>).
        /// </summary>
        public string Outcome => RuleOutcomeTags.ForBatch(FaultedCount > 0, BrokenCount > 0);

        /// <summary>
        /// Summarises a synchronous batch result.
        /// </summary>
        public static BatchSummary From(RuleEvaluationResult result)
        {
            return new BatchSummary(result.Entries.Count, result.BrokenRules.Count, result.Faulted.Count(), result.PassedRules.Count, result.TotalDuration);
        }

        /// <summary>
        /// Summarises an asynchronous batch result.
        /// </summary>
        public static BatchSummary From(AsyncRuleEvaluationResult result)
        {
            return new BatchSummary(result.Entries.Count, result.BrokenRules.Count, result.Faulted.Count(), result.PassedRules.Count, result.TotalDuration);
        }
    }
}
