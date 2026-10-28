using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The subject-typed entry points of <see cref="AsyncBusinessRuleChecker"/>. <see cref="For{TSubject}(TSubject)"/>
    /// returns an <see cref="AsyncSubjectRuleChecker{TSubject}"/> whose batch methods bind every
    /// <see cref="IAsyncBusinessRule{TSubject}"/> to that subject, mirroring <c>rule.For(subject)</c>.
    /// <c>EvaluateEachAsync</c> runs one batch per subject, subjects sequentially, and reports them by position.
    /// <para>The typed batch methods live behind <c>For(subject)</c> rather than beside the 2.x overloads on this
    /// class: a subject parameter accepts any argument by identity, so a 2.x-shaped call such as
    /// <c>CheckBySeverityAsync(context, rules, default, RuleSeverity.Error)</c> would otherwise bind the rule array as
    /// the subject and <c>default</c> as a null rule set.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule&lt;Order&gt;[] rules = { new CustomerMustExistRule(repository), new StockMustBeAvailableRule(inventory) };
    ///
    /// await AsyncBusinessRuleChecker.For(order).CheckAllAsync(context, rules, ct);
    /// AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.For(order).EvaluateAllAsync(rules, ct);
    /// AsyncSubjectEvaluationCollection&lt;Order&gt; each = await AsyncBusinessRuleChecker.EvaluateEachAsync(orders, rules, ct);
    /// </code>
    /// </example>
    public static partial class AsyncBusinessRuleChecker
    {
        /// <summary>
        /// Returns the async batch checker for one subject: its methods bind every typed async rule to
        /// <paramref name="subject"/> and run the batch through the same evaluation core as the non-generic overloads.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <returns>An <see cref="AsyncSubjectRuleChecker{TSubject}"/> bound to <paramref name="subject"/>.</returns>
        public static AsyncSubjectRuleChecker<TSubject> For<TSubject>(TSubject subject)
        {
            return new AsyncSubjectRuleChecker<TSubject>(subject);
        }

        /// <summary>
        /// Evaluates the typed async rule set against every subject with a fresh context, one batch per subject, and
        /// returns the results by position. Subjects are evaluated sequentially; every subject is evaluated.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subjects">The subjects to validate, in order.</param>
        /// <param name="rules">The typed async rules to evaluate against each subject.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>One <see cref="AsyncSubjectEvaluation{TSubject}"/> per subject with batch-level aggregates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="subjects"/> or <paramref name="rules"/> is null.</exception>
        public static Task<AsyncSubjectEvaluationCollection<TSubject>> EvaluateEachAsync<TSubject>(IEnumerable<TSubject> subjects, IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return EvaluateEachAsyncCore(new BusinessRuleContext(), subjects, rules, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <summary>
        /// Evaluates the typed async rule set against every subject using context, one batch per subject, and returns
        /// the results by position. The same context instance is shared by all batches.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context shared by every subject's batch.</param>
        /// <param name="subjects">The subjects to validate, in order.</param>
        /// <param name="rules">The typed async rules to evaluate against each subject.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>One <see cref="AsyncSubjectEvaluation{TSubject}"/> per subject with batch-level aggregates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="subjects"/> or <paramref name="rules"/> is null.</exception>
        public static Task<AsyncSubjectEvaluationCollection<TSubject>> EvaluateEachAsync<TSubject>(BusinessRuleContext context, IEnumerable<TSubject> subjects, IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return EvaluateEachAsyncCore(context, subjects, rules, new AsyncRuleEvaluationOptions(), cancellationToken);
        }

        /// <summary>
        /// Evaluates the typed async rule set against every subject with a fresh context and the supplied options, one
        /// batch per subject. Fail-fast and parallel evaluation apply within a subject's batch, never across subjects;
        /// <see cref="AsyncRuleEvaluationOptions.ThrowOn"/> is ignored.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subjects">The subjects to validate, in order.</param>
        /// <param name="rules">The typed async rules to evaluate against each subject.</param>
        /// <param name="options">The evaluation options applied to each subject's batch.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>One <see cref="AsyncSubjectEvaluation{TSubject}"/> per subject with batch-level aggregates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="subjects"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static Task<AsyncSubjectEvaluationCollection<TSubject>> EvaluateEachAsync<TSubject>(IEnumerable<TSubject> subjects, IEnumerable<IAsyncBusinessRule<TSubject>> rules, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            return EvaluateEachAsyncCore(new BusinessRuleContext(), subjects, rules, options, cancellationToken);
        }

        /// <summary>
        /// Evaluates the typed async rule set against every subject using context and the supplied options, one batch
        /// per subject. Fail-fast and parallel evaluation apply within a subject's batch, never across subjects;
        /// <see cref="AsyncRuleEvaluationOptions.ThrowOn"/> is ignored.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context shared by every subject's batch.</param>
        /// <param name="subjects">The subjects to validate, in order.</param>
        /// <param name="rules">The typed async rules to evaluate against each subject.</param>
        /// <param name="options">The evaluation options applied to each subject's batch.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>One <see cref="AsyncSubjectEvaluation{TSubject}"/> per subject with batch-level aggregates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="subjects"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static Task<AsyncSubjectEvaluationCollection<TSubject>> EvaluateEachAsync<TSubject>(BusinessRuleContext context, IEnumerable<TSubject> subjects, IEnumerable<IAsyncBusinessRule<TSubject>> rules, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            return EvaluateEachAsyncCore(context, subjects, rules, options, cancellationToken);
        }

        private static async Task<AsyncSubjectEvaluationCollection<TSubject>> EvaluateEachAsyncCore<TSubject>(
            BusinessRuleContext context,
            IEnumerable<TSubject> subjects,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            AsyncRuleEvaluationOptions options,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (subjects == null)
            {
                throw new ArgumentNullException(nameof(subjects));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            IReadOnlyList<IAsyncBusinessRule<TSubject>> ruleSet = SubjectBinding.Materialize(rules);
            List<AsyncSubjectEvaluation<TSubject>> evaluations = new List<AsyncSubjectEvaluation<TSubject>>();

            foreach (TSubject subject in subjects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AsyncRuleEvaluationResult result = await EvaluateAllAsyncCore(context, SubjectBinding.Bind(ruleSet, subject), options, RuleSeverity.Info, cancellationToken);
                evaluations.Add(new AsyncSubjectEvaluation<TSubject>(evaluations.Count, subject, result));
            }

            return new AsyncSubjectEvaluationCollection<TSubject>(evaluations);
        }
    }
}
