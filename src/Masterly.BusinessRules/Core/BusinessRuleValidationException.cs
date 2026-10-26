using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Exception thrown when one or more business rules are violated.
    /// Contains the broken rule results that triggered the exception and, when thrown by a batch checker with a
    /// severity threshold, the broken rules below that threshold (<see cref="CollectedRules"/>) and the
    /// evaluation ledger (<see cref="Entries"/>).
    /// </summary>
    public sealed class BusinessRuleValidationException : Exception
    {
        private const string DefaultMessage = "One or more business rules were violated.";

        private static readonly IReadOnlyCollection<BusinessRuleResult> NoResults = Array.Empty<BusinessRuleResult>();
        private static readonly IReadOnlyList<RuleEvaluationEntry> NoEntries = Array.Empty<RuleEvaluationEntry>();

        /// <summary>
        /// Gets the collection of broken rule results that triggered the exception. A result produced by a composed
        /// rule carries its broken members in <see cref="BusinessRuleResult.Details"/>; use <see cref="Flatten"/> to reach the leaves.
        /// </summary>
        public IReadOnlyCollection<BusinessRuleResult> BrokenRules { get; }

        /// <summary>
        /// Gets the broken rule results that were below the severity threshold and therefore did not trigger the
        /// exception by themselves. Empty unless the exception came from a threshold-aware batch call
        /// (<c>CheckAll</c> with <c>ThrowOn</c>, <c>ThrowIfAtLeast</c>, <c>Check(context, throwOn)</c>).
        /// </summary>
        public IReadOnlyCollection<BusinessRuleResult> CollectedRules { get; }

        /// <summary>
        /// Gets the evaluation ledger of the batch that threw, in submission order. Empty unless the exception came
        /// from a batch checker's options-taking overload or from <c>ThrowIfAtLeast</c> on a ledger-backed result.
        /// </summary>
        public IReadOnlyList<RuleEvaluationEntry> Entries { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleValidationException"/> class.
        /// </summary>
        /// <param name="brokenRules">The collection of broken rule results.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="brokenRules"/> is null.</exception>
        public BusinessRuleValidationException(IEnumerable<BusinessRuleResult> brokenRules)
            : base(DefaultMessage)
        {
            if (brokenRules == null)
            {
                throw new ArgumentNullException(nameof(brokenRules));
            }

            BrokenRules = brokenRules.ToList().AsReadOnly();
            CollectedRules = NoResults;
            Entries = NoEntries;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleValidationException"/> class carrying the broken
        /// rules below the severity threshold and the evaluation ledger.
        /// </summary>
        /// <param name="brokenRules">The broken rule results at or above the threshold that triggered the exception.</param>
        /// <param name="collectedRules">The broken rule results below the threshold.</param>
        /// <param name="entries">The evaluation ledger of the batch.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public BusinessRuleValidationException(
            IEnumerable<BusinessRuleResult> brokenRules,
            IEnumerable<BusinessRuleResult> collectedRules,
            IEnumerable<RuleEvaluationEntry> entries)
            : base(DefaultMessage)
        {
            if (brokenRules == null)
            {
                throw new ArgumentNullException(nameof(brokenRules));
            }

            if (collectedRules == null)
            {
                throw new ArgumentNullException(nameof(collectedRules));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            BrokenRules = brokenRules.ToList().AsReadOnly();
            CollectedRules = collectedRules.ToList().AsReadOnly();
            Entries = entries.ToList().AsReadOnly();
        }

        /// <summary>
        /// Returns the broken rule results whose rule carries the tag, compared case-insensitively.
        /// </summary>
        /// <param name="tag">The tag to filter by.</param>
        /// <returns>The matching results from <see cref="BrokenRules"/>, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tag"/> is null.</exception>
        public IEnumerable<BusinessRuleResult> ByTag(string tag)
        {
            return RuleResultFilters.ByTag(BrokenRules, tag);
        }

        /// <summary>
        /// Returns the broken rule results whose rule belongs to the category, compared ordinally.
        /// </summary>
        /// <param name="category">The category to filter by.</param>
        /// <returns>The matching results from <see cref="BrokenRules"/>, in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="category"/> is null.</exception>
        public IEnumerable<BusinessRuleResult> ByCategory(string category)
        {
            return RuleResultFilters.ByCategory(BrokenRules, category);
        }

        /// <summary>
        /// Returns the leaf results of every broken rule: results without <see cref="BusinessRuleResult.Details"/>
        /// are returned as they are, composed results are replaced by their member leaves, recursively.
        /// </summary>
        /// <returns>The leaf results in evaluation order.</returns>
        public IEnumerable<BusinessRuleResult> Flatten()
        {
            return BrokenRules.SelectMany(result => result.Flatten());
        }
    }
}
