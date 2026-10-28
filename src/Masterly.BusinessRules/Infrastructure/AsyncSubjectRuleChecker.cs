using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// The async batch checker for one subject, returned by <see cref="AsyncBusinessRuleChecker.For{TSubject}(TSubject)"/>.
    /// Each method binds every <see cref="IAsyncBusinessRule{TSubject}"/> to <see cref="Subject"/> with
    /// <c>.For(subject)</c> and runs the resulting batch through the same evaluation core as the non-generic
    /// <see cref="AsyncBusinessRuleChecker"/> overloads, so the ledger, fault policy, severity threshold, parallel
    /// evaluation and observer callbacks behave identically; observers receive the
    /// <see cref="BoundAsyncBusinessRule{TSubject}"/> instances. The methods without a context evaluate against a fresh context.
    /// <para>Argument order follows <see cref="AsyncBusinessRuleChecker"/>: a method that takes a context puts it first
    /// (<c>(context, rules, ...)</c>). The filter methods take the cancellation token before the severities or tags, as
    /// the non-generic <c>CheckBySeverityAsync</c> always has. The options overloads are generic in <c>TOptions</c>
    /// (inferred from the argument) so that a bare <c>null</c> or <c>default</c> literal never selects them.</para>
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule&lt;Order&gt;[] rules = { new CustomerMustExistRule(repository), new StockMustBeAvailableRule(inventory) };
    ///
    /// await AsyncBusinessRuleChecker.For(order).CheckAllAsync(context, rules, ct);
    /// AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.For(order).EvaluateAllAsync(rules, new AsyncRuleEvaluationOptions { RunInParallel = true }, ct);
    /// await AsyncBusinessRuleChecker.For(order).CheckBySeverityAsync(context, rules, ct, RuleSeverity.Error);
    /// </code>
    /// </example>
    public sealed class AsyncSubjectRuleChecker<TSubject>
    {
        internal AsyncSubjectRuleChecker(TSubject subject)
        {
            Subject = subject;
        }

        /// <summary>
        /// Gets the subject every batch of this checker is evaluated against.
        /// </summary>
        public TSubject Subject { get; }

        /// <summary>
        /// Checks all typed async rules against the subject with a fresh context and throws if any are broken.
        /// </summary>
        /// <param name="rules">The typed async rules to check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule in <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CheckAllAsync(IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return CheckAllAsync(new BusinessRuleContext(), rules, cancellationToken);
        }

        /// <summary>
        /// Checks all typed async rules against the subject using context and throws if any are broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The typed async rules to check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CheckAllAsync(BusinessRuleContext context, IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return AsyncBusinessRuleChecker.CheckAllAsyncCore(context, SubjectBinding.Bind(rules, Subject), LegacyOptions(), cancellationToken);
        }

        /// <summary>
        /// Checks all typed async rules against the subject with a fresh context and the supplied options, and throws
        /// if any broken rule is at or above <see cref="AsyncRuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>; generic so that a bare <c>null</c> or
        /// <c>default</c> literal binds to the token overload instead.</typeparam>
        /// <param name="rules">The typed async rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy, severity threshold and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CheckAllAsync<TOptions>(IEnumerable<IAsyncBusinessRule<TSubject>> rules, TOptions options, CancellationToken cancellationToken = default)
            where TOptions : AsyncRuleEvaluationOptions
        {
            return CheckAllAsync(new BusinessRuleContext(), rules, options, cancellationToken);
        }

        /// <summary>
        /// Checks all typed async rules against the subject using context and the supplied options, and throws if any
        /// broken rule is at or above <see cref="AsyncRuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>; generic so that a bare <c>null</c> or
        /// <c>default</c> literal binds to the token overload instead.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The typed async rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy, severity threshold and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CheckAllAsync<TOptions>(BusinessRuleContext context, IEnumerable<IAsyncBusinessRule<TSubject>> rules, TOptions options, CancellationToken cancellationToken = default)
            where TOptions : AsyncRuleEvaluationOptions
        {
            return AsyncBusinessRuleChecker.CheckAllAsyncCore(context, SubjectBinding.Bind(rules, Subject), options, cancellationToken);
        }

        /// <summary>
        /// Checks the listed typed async rules against the subject using context and throws if any are broken. A params
        /// form for rules of different classes: <c>For(order).CheckAllAsync(context, new CustomerMustExistRule(r), new StockRule(i))</c>.
        /// There is no cancellation token; pass the rules as a collection to cancel.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rule">The first typed async rule to check.</param>
        /// <param name="others">The remaining typed async rules to check.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CheckAllAsync(BusinessRuleContext context, IAsyncBusinessRule<TSubject> rule, params IAsyncBusinessRule<TSubject>[] others)
        {
            return CheckAllAsync(context, SubjectBinding.Concat(rule, others), CancellationToken.None);
        }

        /// <summary>
        /// Evaluates all typed async rules against the subject with a fresh context and returns detailed results without throwing.
        /// </summary>
        /// <param name="rules">The typed async rules to evaluate.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return EvaluateAllAsync(new BusinessRuleContext(), rules, cancellationToken);
        }

        /// <summary>
        /// Evaluates all typed async rules against the subject using context and returns detailed results without throwing.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The typed async rules to evaluate.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(BusinessRuleContext context, IEnumerable<IAsyncBusinessRule<TSubject>> rules, CancellationToken cancellationToken = default)
        {
            return AsyncBusinessRuleChecker.EvaluateAllAsyncCore(context, SubjectBinding.Bind(rules, Subject), new AsyncRuleEvaluationOptions(), RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Evaluates all typed async rules against the subject with a fresh context and the supplied options. Never throws for broken rules.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>; generic so that a bare <c>null</c> or
        /// <c>default</c> literal binds to the token overload instead.</typeparam>
        /// <param name="rules">The typed async rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TOptions>(IEnumerable<IAsyncBusinessRule<TSubject>> rules, TOptions options, CancellationToken cancellationToken = default)
            where TOptions : AsyncRuleEvaluationOptions
        {
            return EvaluateAllAsync(new BusinessRuleContext(), rules, options, cancellationToken);
        }

        /// <summary>
        /// Evaluates all typed async rules against the subject using context and the supplied options. Never throws for broken rules.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>; generic so that a bare <c>null</c> or
        /// <c>default</c> literal binds to the token overload instead.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The typed async rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TOptions>(BusinessRuleContext context, IEnumerable<IAsyncBusinessRule<TSubject>> rules, TOptions options, CancellationToken cancellationToken = default)
            where TOptions : AsyncRuleEvaluationOptions
        {
            return AsyncBusinessRuleChecker.EvaluateAllAsyncCore(context, SubjectBinding.Bind(rules, Subject), options, RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Evaluates the listed typed async rules against the subject using context. Never throws for broken rules.
        /// A params form for rules of different classes; there is no cancellation token.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rule">The first typed async rule to evaluate.</param>
        /// <param name="others">The remaining typed async rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="others"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a rule is null.</exception>
        public Task<AsyncRuleEvaluationResult> EvaluateAllAsync(BusinessRuleContext context, IAsyncBusinessRule<TSubject> rule, params IAsyncBusinessRule<TSubject>[] others)
        {
            return EvaluateAllAsync(context, SubjectBinding.Concat(rule, others), CancellationToken.None);
        }

        /// <summary>
        /// Checks only the typed async rules that match the specified severity levels against the subject.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The typed async rules to filter and check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <param name="severities">The severity levels to include.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CheckBySeverityAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            CancellationToken cancellationToken = default,
            params RuleSeverity[] severities)
        {
            List<IAsyncBusinessRule> bound = SubjectBinding.Bind(rules, Subject);
            bound.RemoveAll(rule => !severities.Contains(rule.Severity));
            return AsyncBusinessRuleChecker.CheckAllAsyncCore(context, bound, LegacyOptions(), cancellationToken);
        }

        /// <summary>
        /// Checks only the typed async rules that match the specified category against the subject.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The typed async rules to filter and check.</param>
        /// <param name="category">The category to filter by (ordinal comparison).</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CheckByCategoryAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            string category,
            CancellationToken cancellationToken = default)
        {
            List<IAsyncBusinessRule> bound = SubjectBinding.Bind(rules, Subject);
            bound.RemoveAll(rule => rule.Category != category);
            return AsyncBusinessRuleChecker.CheckAllAsyncCore(context, bound, LegacyOptions(), cancellationToken);
        }

        /// <summary>
        /// Checks only the typed async rules that have any of the specified tags against the subject.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The typed async rules to filter and check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <param name="tags">The tags to filter by (case-insensitive).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="rules"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CheckByTagsAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule<TSubject>> rules,
            CancellationToken cancellationToken = default,
            params string[] tags)
        {
            HashSet<string> tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            List<IAsyncBusinessRule> bound = SubjectBinding.Bind(rules, Subject);
            bound.RemoveAll(rule => !rule.Tags.Any(tag => tagSet.Contains(tag)));
            return AsyncBusinessRuleChecker.CheckAllAsyncCore(context, bound, LegacyOptions(), cancellationToken);
        }

        private static AsyncRuleEvaluationOptions LegacyOptions()
        {
            return AsyncBusinessRuleChecker.LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null);
        }
    }
}
