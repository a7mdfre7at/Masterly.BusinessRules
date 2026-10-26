using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Represents the result of evaluating a collection of synchronous business rules.
    /// Provides access to both broken and passed rules, severity filters and, when produced by
    /// <see cref="BusinessRuleChecker"/>, the per-rule evaluation ledger (<see cref="Entries"/>).
    /// </summary>
    public sealed class RuleEvaluationResult
    {
        private static readonly IReadOnlyList<RuleEvaluationEntry> NoEntries = Array.Empty<RuleEvaluationEntry>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleEvaluationResult"/> class from explicit broken and passed
        /// collections. A result built this way has no ledger: <see cref="Entries"/> is empty and
        /// <see cref="TotalDuration"/> is zero.
        /// </summary>
        /// <param name="brokenRules">The collection of broken rule results.</param>
        /// <param name="passedRules">The collection of rules that passed evaluation.</param>
        /// <exception cref="ArgumentNullException">Thrown when either collection is null.</exception>
        public RuleEvaluationResult(
            IEnumerable<BusinessRuleResult> brokenRules,
            IEnumerable<IBusinessRule> passedRules)
        {
            if (brokenRules == null)
            {
                throw new ArgumentNullException(nameof(brokenRules));
            }

            if (passedRules == null)
            {
                throw new ArgumentNullException(nameof(passedRules));
            }

            BrokenRules = brokenRules.ToList().AsReadOnly();
            PassedRules = passedRules.ToList().AsReadOnly();
            Entries = NoEntries;
            TotalDuration = TimeSpan.Zero;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleEvaluationResult"/> class from an evaluation ledger.
        /// <see cref="BrokenRules"/> holds the <see cref="RuleEvaluationEntry.Result"/> of every entry that has one and
        /// <see cref="PassedRules"/> the rule of every <see cref="RuleOutcome.Passed"/> entry, both in ledger order.
        /// </summary>
        /// <param name="entries">The ledger entries, in submission order; every entry must carry a synchronous <see cref="RuleEvaluationEntry.Rule"/>.</param>
        /// <param name="totalDuration">The wall-clock duration of the whole batch.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when an entry does not carry a synchronous rule.</exception>
        public RuleEvaluationResult(IEnumerable<RuleEvaluationEntry> entries, TimeSpan totalDuration)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            List<RuleEvaluationEntry> ledger = entries.ToList();
            List<BusinessRuleResult> brokenRules = new List<BusinessRuleResult>();
            List<IBusinessRule> passedRules = new List<IBusinessRule>();

            foreach (RuleEvaluationEntry entry in ledger)
            {
                if (entry.Rule == null)
                {
                    throw new ArgumentException("Every entry of a synchronous evaluation result must carry a synchronous rule.", nameof(entries));
                }

                if (entry.Result != null)
                {
                    brokenRules.Add(entry.Result);
                }
                else if (entry.Outcome == RuleOutcome.Passed)
                {
                    passedRules.Add(entry.Rule);
                }
            }

            Entries = ledger.AsReadOnly();
            BrokenRules = brokenRules.AsReadOnly();
            PassedRules = passedRules.AsReadOnly();
            TotalDuration = totalDuration;
        }

        /// <summary>
        /// Gets all broken rule results.
        /// </summary>
        public IReadOnlyCollection<BusinessRuleResult> BrokenRules { get; }

        /// <summary>
        /// Gets all rules that passed evaluation.
        /// </summary>
        public IReadOnlyCollection<IBusinessRule> PassedRules { get; }

        /// <summary>
        /// Gets the evaluation ledger: one entry per submitted rule, in submission order, with its outcome, duration
        /// and fault. Empty for a result built from explicit broken/passed collections.
        /// </summary>
        public IReadOnlyList<RuleEvaluationEntry> Entries { get; }

        /// <summary>
        /// Gets the entries of rules that threw during evaluation.
        /// </summary>
        public IEnumerable<RuleEvaluationEntry> Faulted => Entries.Where(entry => entry.IsFaulted);

        /// <summary>
        /// Returns true if any rule threw during evaluation.
        /// </summary>
        public bool HasFaults => Faulted.Any();

        /// <summary>
        /// Gets the wall-clock duration of the whole batch. Zero for a result built without a ledger.
        /// </summary>
        public TimeSpan TotalDuration { get; }

        /// <summary>
        /// Gets the entry with the longest <see cref="RuleEvaluationEntry.Duration"/>, or null when the ledger is empty.
        /// Ties resolve to the earliest entry.
        /// </summary>
        public RuleEvaluationEntry? Slowest
        {
            get
            {
                RuleEvaluationEntry? slowest = null;
                foreach (RuleEvaluationEntry entry in Entries)
                {
                    if (slowest == null || entry.Duration > slowest.Duration)
                    {
                        slowest = entry;
                    }
                }

                return slowest;
            }
        }

        /// <summary>
        /// Returns true if any rules were broken.
        /// </summary>
        public bool HasBrokenRules => BrokenRules.Count > 0;

        /// <summary>
        /// Returns true if all rules passed.
        /// </summary>
        public bool AllPassed => BrokenRules.Count == 0;

        /// <summary>
        /// Gets broken rules filtered by the specified severity level.
        /// </summary>
        /// <param name="severity">The severity level to filter by.</param>
        /// <returns>An enumerable of broken rule results matching the specified severity.</returns>
        public IEnumerable<BusinessRuleResult> GetBySeverity(RuleSeverity severity)
            => BrokenRules.Where(r => r.Severity == severity);

        /// <summary>
        /// Gets broken rules whose rule carries the tag, compared case-insensitively.
        /// </summary>
        /// <param name="tag">The tag to filter by.</param>
        /// <returns>The matching results from <see cref="BrokenRules"/>, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tag"/> is null.</exception>
        public IEnumerable<BusinessRuleResult> ByTag(string tag)
            => RuleResultFilters.ByTag(BrokenRules, tag);

        /// <summary>
        /// Gets broken rules whose rule belongs to the category, compared ordinally.
        /// </summary>
        /// <param name="category">The category to filter by.</param>
        /// <returns>The matching results from <see cref="BrokenRules"/>, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="category"/> is null.</exception>
        public IEnumerable<BusinessRuleResult> ByCategory(string category)
            => RuleResultFilters.ByCategory(BrokenRules, category);

        /// <summary>
        /// Gets only error-level broken rules.
        /// </summary>
        public IEnumerable<BusinessRuleResult> Errors
            => GetBySeverity(RuleSeverity.Error);

        /// <summary>
        /// Gets only warning-level broken rules.
        /// </summary>
        public IEnumerable<BusinessRuleResult> Warnings
            => GetBySeverity(RuleSeverity.Warning);

        /// <summary>
        /// Gets only info-level broken rules.
        /// </summary>
        public IEnumerable<BusinessRuleResult> Infos
            => GetBySeverity(RuleSeverity.Info);

        /// <summary>
        /// Returns true if there are any error-level broken rules.
        /// </summary>
        public bool HasErrors => Errors.Any();

        /// <summary>
        /// Returns true if there are any warning-level broken rules.
        /// </summary>
        public bool HasWarnings => Warnings.Any();

        /// <summary>
        /// Throws a <see cref="BusinessRuleValidationException"/> if any rules are broken.
        /// </summary>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public void ThrowIfBroken()
        {
            ThrowIfAtLeast(RuleSeverity.Info);
        }

        /// <summary>
        /// Throws a <see cref="BusinessRuleValidationException"/> only if there are error-level broken rules.
        /// Warnings and info-level violations are ignored.
        /// </summary>
        /// <exception cref="BusinessRuleValidationException">Thrown when there are error-level broken rules.</exception>
        public void ThrowIfErrors()
        {
            ThrowIfAtLeast(RuleSeverity.Error);
        }

        /// <summary>
        /// Throws a <see cref="BusinessRuleValidationException"/> if any broken rule has a severity at or above
        /// <paramref name="severity"/>. The exception's <see cref="BusinessRuleValidationException.BrokenRules"/> holds the
        /// results at or above the threshold, <see cref="BusinessRuleValidationException.CollectedRules"/> the broken results
        /// below it, and <see cref="BusinessRuleValidationException.Entries"/> this result's ledger.
        /// </summary>
        /// <param name="severity">The lowest severity that triggers the exception.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        public void ThrowIfAtLeast(RuleSeverity severity)
        {
            RuleSeverityThreshold.Split(BrokenRules, severity, out List<BusinessRuleResult> thrown, out List<BusinessRuleResult> collected);

            if (thrown.Count > 0)
            {
                throw new BusinessRuleValidationException(thrown, collected, Entries);
            }
        }
    }
}
