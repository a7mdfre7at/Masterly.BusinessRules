using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The positional list returned by <c>AsyncBusinessRuleChecker.EvaluateEachAsync</c>: one
    /// <see cref="AsyncSubjectEvaluation{TSubject}"/> per input subject, in input order, plus the aggregates
    /// <see cref="AllPassed"/>, <see cref="HasBrokenRules"/>, <see cref="Broken"/>, <see cref="Passed"/>,
    /// <see cref="BrokenRules"/> and <see cref="TotalDuration"/>. An empty batch reports <see cref="AllPassed"/> as true.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public sealed class AsyncSubjectEvaluationCollection<TSubject> : IReadOnlyList<AsyncSubjectEvaluation<TSubject>>
    {
        private readonly IReadOnlyList<AsyncSubjectEvaluation<TSubject>> _evaluations;

        /// <summary>
        /// Initializes a new collection over the given per-subject evaluations.
        /// </summary>
        /// <param name="evaluations">The evaluations in subject order.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluations"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when an evaluation in the sequence is null.</exception>
        public AsyncSubjectEvaluationCollection(IEnumerable<AsyncSubjectEvaluation<TSubject>> evaluations)
        {
            if (evaluations == null)
            {
                throw new ArgumentNullException(nameof(evaluations));
            }

            List<AsyncSubjectEvaluation<TSubject>> list = evaluations.ToList();
            if (list.Any(evaluation => evaluation == null))
            {
                throw new ArgumentException("The evaluations cannot contain null.", nameof(evaluations));
            }

            _evaluations = list;
        }

        /// <inheritdoc />
        public int Count => _evaluations.Count;

        /// <inheritdoc />
        public AsyncSubjectEvaluation<TSubject> this[int index] => _evaluations[index];

        /// <summary>
        /// Gets whether every rule passed for every subject. True for an empty batch.
        /// </summary>
        public bool AllPassed => _evaluations.All(evaluation => evaluation.AllPassed);

        /// <summary>
        /// Gets whether at least one rule is broken for at least one subject.
        /// </summary>
        public bool HasBrokenRules => _evaluations.Any(evaluation => evaluation.HasBrokenRules);

        /// <summary>
        /// Gets the evaluations of the subjects with at least one broken rule, in subject order.
        /// </summary>
        public IEnumerable<AsyncSubjectEvaluation<TSubject>> Broken => _evaluations.Where(evaluation => evaluation.HasBrokenRules);

        /// <summary>
        /// Gets the evaluations of the subjects for which every rule passed, in subject order.
        /// </summary>
        public IEnumerable<AsyncSubjectEvaluation<TSubject>> Passed => _evaluations.Where(evaluation => evaluation.AllPassed);

        /// <summary>
        /// Gets every broken rule result across all subjects, in subject order then rule order.
        /// </summary>
        public IEnumerable<BusinessRuleResult> BrokenRules => _evaluations.SelectMany(evaluation => evaluation.BrokenRules);

        /// <summary>
        /// Gets the sum of the per-subject batch durations.
        /// </summary>
        public TimeSpan TotalDuration => new TimeSpan(_evaluations.Sum(evaluation => evaluation.Result.TotalDuration.Ticks));

        /// <inheritdoc />
        public IEnumerator<AsyncSubjectEvaluation<TSubject>> GetEnumerator() => _evaluations.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
