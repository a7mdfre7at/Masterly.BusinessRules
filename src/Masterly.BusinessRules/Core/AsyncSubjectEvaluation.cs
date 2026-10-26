using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The outcome of evaluating an async rule set against one subject of an <c>EvaluateEachAsync</c> batch: the
    /// subject, its position in the input sequence and the full <see cref="AsyncRuleEvaluationResult"/>.
    /// Subjects are identified by position, never by equality, so duplicate or value-type subjects are reported separately.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class AsyncSubjectEvaluation<TSubject>
    {
        /// <summary>
        /// Initializes a new <see cref="AsyncSubjectEvaluation{TSubject}"/>.
        /// </summary>
        /// <param name="index">The zero-based position of the subject in the evaluated sequence.</param>
        /// <param name="subject">The evaluated subject.</param>
        /// <param name="result">The evaluation result of the rule set for this subject.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
        public AsyncSubjectEvaluation(int index, TSubject subject, AsyncRuleEvaluationResult result)
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
        public AsyncRuleEvaluationResult Result { get; }

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
