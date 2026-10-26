using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The outcome of evaluating a rule set against one subject of an <c>EvaluateEach</c> batch: the subject, its
    /// position in the input sequence and the full <see cref="RuleEvaluationResult"/> (broken and passed rules, ledger).
    /// Subjects are identified by position, never by equality, so duplicate or value-type subjects are reported separately.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class SubjectEvaluation<TSubject>
    {
        /// <summary>
        /// Initializes a new <see cref="SubjectEvaluation{TSubject}"/>.
        /// </summary>
        /// <param name="index">The zero-based position of the subject in the evaluated sequence.</param>
        /// <param name="subject">The evaluated subject.</param>
        /// <param name="result">The evaluation result of the rule set for this subject.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
        public SubjectEvaluation(int index, TSubject subject, RuleEvaluationResult result)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The subject index cannot be negative.");
            }

            Index = index;
            Subject = subject;
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// Gets the zero-based position of the subject in the evaluated sequence.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the evaluated subject.
        /// </summary>
        public TSubject Subject { get; }

        /// <summary>
        /// Gets the evaluation result of the rule set for this subject, including its ledger.
        /// </summary>
        public RuleEvaluationResult Result { get; }

        /// <summary>
        /// Gets whether every rule passed for this subject.
        /// </summary>
        public bool AllPassed => Result.AllPassed;

        /// <summary>
        /// Gets whether at least one rule is broken for this subject.
        /// </summary>
        public bool HasBrokenRules => Result.HasBrokenRules;

        /// <summary>
        /// Gets the broken rule results for this subject.
        /// </summary>
        public IReadOnlyCollection<BusinessRuleResult> BrokenRules => Result.BrokenRules;
    }
}
