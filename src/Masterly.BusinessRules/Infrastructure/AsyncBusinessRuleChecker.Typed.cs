using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Subject-typed overloads of <see cref="AsyncBusinessRuleChecker"/>. Each one binds every
    /// <see cref="IAsyncBusinessRule{TSubject}"/> to the subject with <c>.For(subject)</c> and runs the resulting batch
    /// through the same evaluation core as the non-generic overloads, so the ledger, fault policy, severity threshold,
    /// parallel evaluation and observer callbacks behave identically; observers receive the
    /// <see cref="BoundAsyncBusinessRule{TSubject}"/> instances. The overloads without a context evaluate against a fresh context.
    /// <c>EvaluateEachAsync</c> runs one batch per subject, subjects sequentially, and reports them by position.
    /// <para>Argument order: an overload that takes a context puts it first, as every <see cref="AsyncBusinessRuleChecker"/>
    /// overload does (<c>(context, subject, rules, ...)</c>); the context-less overloads start with the subject. This
    /// differs from the sync <see cref="BusinessRuleChecker"/> (<c>(subject, rules, context)</c>) and from
    /// <c>IRuleRunner&lt;T&gt;.CheckAllAsync(subject, context, ...)</c> in the DependencyInjection package.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule&lt;Order&gt;[] rules = { new CustomerMustExistRule(repository), new StockMustBeAvailableRule(inventory) };
    ///
    /// await AsyncBusinessRuleChecker.CheckAllAsync(context, order, rules, ct);
    /// AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(order, rules, ct);
    /// AsyncSubjectEvaluationCollection&lt;Order&gt; each = await AsyncBusinessRuleChecker.EvaluateEachAsync(orders, rules, ct);
    /// </code>
    /// </example>
    public static partial class AsyncBusinessRuleChecker
    {
        /// <summary>
        /// Checks all typed async rules against the subject with a fresh context and throws if any are broken.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule in <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckAllAsync<TSubject>(TSubject subject, IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return CheckAllAsyncCore(new BusinessRuleContext(), SubjectBinding.Bind(rules, subject), LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken);
        }

        /// <summary>
        /// Checks all typed async rules against the subject using context and throws if any are broken.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckAllAsync<TSubject>(BusinessRuleContext context, TSubject subject, IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return CheckAllAsyncCore(context, SubjectBinding.Bind(rules, subject), LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken);
        }

        /// <summary>
        /// Checks all typed async rules against the subject with a fresh context and the supplied options, and throws
        /// if any broken rule is at or above <see cref="AsyncRuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy, severity threshold and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckAllAsync<TSubject>(TSubject subject, IEnumerable<IAsyncBusinessRule<TSubject>> rules, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            return CheckAllAsyncCore(new BusinessRuleContext(), SubjectBinding.Bind(rules, subject), options, cancellationToken);
        }

        /// <summary>
        /// Checks all typed async rules against the subject using context and the supplied options, and throws if any
        /// broken rule is at or above <see cref="AsyncRuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy, severity threshold and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckAllAsync<TSubject>(BusinessRuleContext context, TSubject subject, IEnumerable<IAsyncBusinessRule<TSubject>> rules, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            return CheckAllAsyncCore(context, SubjectBinding.Bind(rules, subject), options, cancellationToken);
        }

        /// <summary>
        /// Evaluates all typed async rules against the subject with a fresh context and returns detailed results without throwing.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to evaluate.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        public static Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TSubject>(TSubject subject, IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsyncCore(new BusinessRuleContext(), SubjectBinding.Bind(rules, subject), new AsyncRuleEvaluationOptions(), RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Evaluates all typed async rules against the subject using context and returns detailed results without throwing.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to evaluate.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        public static Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TSubject>(BusinessRuleContext context, TSubject subject, IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsyncCore(context, SubjectBinding.Bind(rules, subject), new AsyncRuleEvaluationOptions(), RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Evaluates all typed async rules against the subject with a fresh context and the supplied options. Never throws for broken rules.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TSubject>(TSubject subject, IEnumerable<IAsyncBusinessRule<TSubject>> rules, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsyncCore(new BusinessRuleContext(), SubjectBinding.Bind(rules, subject), options, RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Evaluates all typed async rules against the subject using context and the supplied options. Never throws for broken rules.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TSubject>(BusinessRuleContext context, TSubject subject, IEnumerable<IAsyncBusinessRule<TSubject>> rules, AsyncRuleEvaluationOptions options, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsyncCore(context, SubjectBinding.Bind(rules, subject), options, RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Checks the listed typed async rules against the subject using context and throws if any are broken. A params
        /// form for rules of different classes: <c>CheckAllAsync(context, order, new CustomerMustExistRule(r), new StockRule(i))</c>.
        /// There is no cancellation token; pass the rules as a collection to cancel.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rule">The first typed async rule to check.</param>
        /// <param name="others">The remaining typed async rules to check.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckAllAsync<TSubject>(BusinessRuleContext context, TSubject subject, IAsyncBusinessRule<TSubject> rule, params IAsyncBusinessRule<TSubject>[] others)
        {
            return CheckAllAsync(context, subject, SubjectBinding.Concat(rule, others), CancellationToken.None);
        }

        /// <summary>
        /// Evaluates the listed typed async rules against the subject using context. Never throws for broken rules.
        /// A params form for rules of different classes; there is no cancellation token.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rule">The first typed async rule to evaluate.</param>
        /// <param name="others">The remaining typed async rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule is null.</exception>
        public static Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TSubject>(BusinessRuleContext context, TSubject subject, IAsyncBusinessRule<TSubject> rule, params IAsyncBusinessRule<TSubject>[] others)
        {
            return EvaluateAllAsync(context, subject, SubjectBinding.Concat(rule, others), CancellationToken.None);
        }

        /// <summary>
        /// Checks the typed async rules that match the listed severity levels against the subject, without a
        /// cancellation token, so the severities follow the rules directly as in the sync <c>CheckBySeverity</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to filter and check.</param>
        /// <param name="severities">The severity levels to include.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckBySeverityAsync<TSubject>(
            BusinessRuleContext context,
            TSubject subject,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            params RuleSeverity[] severities)
        {
            return CheckBySeverityAsync(context, subject, rules, CancellationToken.None, severities);
        }

        /// <summary>
        /// Checks the typed async rules that have any of the listed tags against the subject, without a cancellation
        /// token, so the tags follow the rules directly as in the sync <c>CheckByTags</c>.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to filter and check.</param>
        /// <param name="tags">The tags to filter by (case-insensitive).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckByTagsAsync<TSubject>(
            BusinessRuleContext context,
            TSubject subject,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            params string[] tags)
        {
            return CheckByTagsAsync(context, subject, rules, CancellationToken.None, tags);
        }

        /// <summary>
        /// Checks only the typed async rules that match the specified severity levels against the subject.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to filter and check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <param name="severities">The severity levels to include.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckBySeverityAsync<TSubject>(
            BusinessRuleContext context,
            TSubject subject,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            CancellationToken cancellationToken = default,
            params RuleSeverity[] severities)
        {
            List<IAsyncBusinessRule> bound = SubjectBinding.Bind(rules, subject);
            bound.RemoveAll(rule => !severities.Contains(rule.Severity));
            return CheckAllAsyncCore(context, bound, LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken);
        }

        /// <summary>
        /// Checks only the typed async rules that match the specified category against the subject.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to filter and check.</param>
        /// <param name="category">The category to filter by (ordinal comparison).</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckByCategoryAsync<TSubject>(
            BusinessRuleContext context,
            TSubject subject,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            string category,
            CancellationToken cancellationToken = default)
        {
            List<IAsyncBusinessRule> bound = SubjectBinding.Bind(rules, subject);
            bound.RemoveAll(rule => rule.Category != category);
            return CheckAllAsyncCore(context, bound, LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken);
        }

        /// <summary>
        /// Checks only the typed async rules that have any of the specified tags against the subject.
        /// </summary>
        /// <typeparam name="TSubject">The subject type.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="rules">The typed async rules to filter and check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <param name="tags">The tags to filter by (case-insensitive).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckByTagsAsync<TSubject>(
            BusinessRuleContext context,
            TSubject subject,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            CancellationToken cancellationToken = default,
            params string[] tags)
        {
            HashSet<string> tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            List<IAsyncBusinessRule> bound = SubjectBinding.Bind(rules, subject);
            bound.RemoveAll(rule => !rule.Tags.Any(tag => tagSet.Contains(tag)));
            return CheckAllAsyncCore(context, bound, LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken);
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
