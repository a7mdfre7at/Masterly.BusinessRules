using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// An immutable snapshot of a <see cref="RuleCoverageSession"/>: one <see cref="RuleCoverageEntry"/> per rule code,
    /// the aggregate <see cref="OutcomeCoverage"/> (observed outcomes over <c>2 × rules</c>), the lists of codes that were
    /// never broken, never passed or never evaluated, a printable <see cref="ToText"/> and the
    /// <see cref="AssertMinimum"/> gate for test suites.
    /// </summary>
    public sealed class RuleCoverageReport
    {
        private static readonly IReadOnlyList<string> NoCodes = Array.Empty<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleCoverageReport"/> class.
        /// </summary>
        /// <param name="entries">The per-code entries; sorted ordinally by code.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when an entry is null or two entries share a code.</exception>
        public RuleCoverageReport(IEnumerable<RuleCoverageEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            List<RuleCoverageEntry> sorted = new List<RuleCoverageEntry>();
            HashSet<string> codes = new HashSet<string>(StringComparer.Ordinal);

            foreach (RuleCoverageEntry entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException(RuleCoverageText.NullEntry, nameof(entries));
                }

                if (!codes.Add(entry.Code))
                {
                    throw new ArgumentException(RuleCoverageText.DuplicateCode(entry.Code), nameof(entries));
                }

                sorted.Add(entry);
            }

            sorted.Sort((left, right) => string.CompareOrdinal(left.Code, right.Code));
            Entries = sorted;

            NeverBroken = Codes(sorted, entry => entry.WasEvaluated && !entry.SeenBroken);
            NeverPassed = Codes(sorted, entry => entry.WasEvaluated && !entry.SeenPassed);
            NeverEvaluated = Codes(sorted, entry => !entry.WasEvaluated);
            FullyCoveredCount = sorted.Count(entry => entry.IsFullyCovered);
            CoveredOutcomes = sorted.Sum(entry => entry.CoveredOutcomes);
        }

        /// <summary>
        /// Gets the per-code entries sorted ordinally by code.
        /// </summary>
        public IReadOnlyList<RuleCoverageEntry> Entries { get; }

        /// <summary>
        /// Gets the number of registered rule codes.
        /// </summary>
        public int RuleCount => Entries.Count;

        /// <summary>
        /// Gets the number of rules seen both broken and passing.
        /// </summary>
        public int FullyCoveredCount { get; }

        /// <summary>
        /// Gets the number of distinct outcomes observed across all rules.
        /// </summary>
        public int CoveredOutcomes { get; }

        /// <summary>
        /// Gets the number of outcomes that could have been observed: <see cref="RuleCoverageEntry.OutcomesPerRule"/> per rule.
        /// </summary>
        public int TotalOutcomes => RuleCount * RuleCoverageEntry.OutcomesPerRule;

        /// <summary>
        /// Gets the observed share of outcomes in <c>0.0..1.0</c>. A report with no rules has a coverage of <c>0.0</c>:
        /// nothing was covered, which usually means no rule was wrapped with <c>WithCoverage</c>.
        /// </summary>
        public double OutcomeCoverage => RuleCount == 0 ? 0.0 : CoveredOutcomes / (double)TotalOutcomes;

        /// <summary>
        /// Gets a value indicating whether every rule was seen both broken and passing.
        /// </summary>
        public bool IsComplete => RuleCount > 0 && FullyCoveredCount == RuleCount;

        /// <summary>
        /// Gets the codes of rules that were evaluated but never seen broken.
        /// </summary>
        public IReadOnlyList<string> NeverBroken { get; }

        /// <summary>
        /// Gets the codes of rules that were evaluated but never seen passing (never exercised on the happy path).
        /// </summary>
        public IReadOnlyList<string> NeverPassed { get; }

        /// <summary>
        /// Gets the codes of rules that were wrapped but never evaluated.
        /// </summary>
        public IReadOnlyList<string> NeverEvaluated { get; }

        /// <summary>
        /// Finds the entry for a code.
        /// </summary>
        /// <param name="code">The rule code.</param>
        /// <returns>The entry, or null when the code was never registered.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is null.</exception>
        public RuleCoverageEntry? Find(string code)
        {
            if (code == null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            foreach (RuleCoverageEntry entry in Entries)
            {
                if (string.Equals(entry.Code, code, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Throws when <see cref="OutcomeCoverage"/> is below <paramref name="outcomeCoverage"/>. The exception message
        /// names the uncovered rules and includes <see cref="ToText"/>.
        /// </summary>
        /// <param name="outcomeCoverage">The required share of outcomes in <c>0.0..1.0</c>; <c>1.0</c> means every rule was seen both broken and passing.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="outcomeCoverage"/> is outside <c>0.0..1.0</c>.</exception>
        /// <exception cref="RuleAssertionException">Thrown when the observed coverage is below the minimum.</exception>
        public void AssertMinimum(double outcomeCoverage)
        {
            if (double.IsNaN(outcomeCoverage) || outcomeCoverage < 0.0 || outcomeCoverage > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(outcomeCoverage), outcomeCoverage, RuleCoverageText.MinimumOutOfRange);
            }

            if (OutcomeCoverage < outcomeCoverage)
            {
                throw new RuleAssertionException(RuleCoverageText.BelowMinimum(this, outcomeCoverage));
            }
        }

        /// <summary>
        /// Renders one line per rule in the form <c>CODE  broken ✔  passed ✘   &lt;- never exercised on the happy path</c>
        /// followed by a summary line.
        /// </summary>
        /// <returns>The rendered report without a trailing newline.</returns>
        public string ToText()
        {
            return RuleCoverageText.FormatReport(this);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToText();
        }

        private static IReadOnlyList<string> Codes(List<RuleCoverageEntry> entries, Func<RuleCoverageEntry, bool> predicate)
        {
            List<string> codes = entries.Where(predicate).Select(entry => entry.Code).ToList();
            return codes.Count == 0 ? NoCodes : codes;
        }
    }
}
