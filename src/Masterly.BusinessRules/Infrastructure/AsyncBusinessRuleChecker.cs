using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Static utility class for batch validation of asynchronous business rules.
    /// Provides methods for checking multiple async rules with support for parallel execution,
    /// fail-fast behavior, and observer notifications.
    /// Every overload runs through one core that records an evaluation ledger (<see cref="AsyncRuleEvaluationResult.Entries"/>);
    /// the overloads taking <see cref="AsyncRuleEvaluationOptions"/> add a fault policy, a severity threshold and observer
    /// callbacks from both <c>CheckAllAsync</c> and <c>EvaluateAllAsync</c>.
    /// A rule that fails to read the context (<see cref="ContextKeyNotFoundException"/> / <see cref="ContextValueTypeMismatchException"/>)
    /// is reported as a <see cref="BusinessRuleContextException"/> naming the rule's <c>Code</c>, with the original exception preserved
    /// as its inner exception; the fault policy then applies to the wrapped exception. Cancellation is never wrapped.
    /// Violations observed by <see cref="RuleRolloutMode.Shadow"/> gated rules are collected on
    /// <see cref="AsyncRuleEvaluationResult.ShadowResults"/> and reported through <see cref="IAsyncRuleExecutionObserver.OnShadowViolationAsync"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// // Basic sequential execution
    /// await AsyncBusinessRuleChecker.CheckAllAsync(context, rule1, rule2);
    ///
    /// // Parallel execution with observer
    /// await AsyncBusinessRuleChecker.CheckAllAsync(
    ///     context, rules,
    ///     runInParallel: true,
    ///     observer: myObserver);
    ///
    /// // Evaluate without throwing
    /// AsyncRuleEvaluationResult result = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, rules);
    ///
    /// // Options object: parallel, record faults, observe
    /// result = await AsyncBusinessRuleChecker.EvaluateAllAsync(context, rules,
    ///     new AsyncRuleEvaluationOptions { RunInParallel = true, OnFault = FaultPolicy.Record, Observer = myObserver }, ct);
    /// </code>
    /// </example>
    public static partial class AsyncBusinessRuleChecker
    {
        /// <summary>
        /// Checks all async rules sequentially and throws if any are broken.
        /// </summary>
        /// <param name="rules">The async rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckAllAsync(params IAsyncBusinessRule[] rules)
        {
            await CheckAllAsyncCore(new BusinessRuleContext(), rules, LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken: default);
        }

        /// <summary>
        /// Checks all async rules with fail-fast option and throws if any are broken.
        /// </summary>
        /// <param name="stopOnFirstFailure">If true, stops checking after the first broken rule.</param>
        /// <param name="rules">The async rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckAllAsync(bool stopOnFirstFailure, params IAsyncBusinessRule[] rules)
        {
            await CheckAllAsyncCore(new BusinessRuleContext(), rules, LegacyOptions(stopOnFirstFailure, runInParallel: false, observer: null), cancellationToken: default);
        }

        /// <summary>
        /// Checks all async rules sequentially and throws if any are broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckAllAsync(BusinessRuleContext context, params IAsyncBusinessRule[] rules)
        {
            await CheckAllAsyncCore(context, rules, LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken: default);
        }

        /// <summary>
        /// Checks all async rules with fail-fast option and throws if any are broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="stopOnFirstFailure">If true, stops checking after the first broken rule.</param>
        /// <param name="rules">The async rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckAllAsync(BusinessRuleContext context, bool stopOnFirstFailure, params IAsyncBusinessRule[] rules)
        {
            await CheckAllAsyncCore(context, rules, LegacyOptions(stopOnFirstFailure, runInParallel: false, observer: null), cancellationToken: default);
        }

        /// <summary>
        /// Checks all async rules with options for fail-fast and parallel execution.
        /// </summary>
        /// <param name="rules">The async rules to check.</param>
        /// <param name="stopOnFirstFailure">If true, stops checking after the first broken rule. Cannot be combined with parallel execution.</param>
        /// <param name="runInParallel">If true, evaluates all rules in parallel for better performance.</param>
        /// <param name="observer">Optional observer for receiving async rule evaluation callbacks.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckAllAsync(
            IEnumerable<IAsyncBusinessRule> rules,
            bool stopOnFirstFailure = false,
            bool runInParallel = false,
            IAsyncRuleExecutionObserver? observer = null,
            CancellationToken cancellationToken = default)
        {
            await CheckAllAsyncCore(new BusinessRuleContext(), rules, LegacyOptions(stopOnFirstFailure, runInParallel, observer), cancellationToken);
        }

        /// <summary>
        /// Checks all async rules with options for fail-fast and parallel execution.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to check.</param>
        /// <param name="stopOnFirstFailure">If true, stops checking after the first broken rule. Cannot be combined with parallel execution.</param>
        /// <param name="runInParallel">If true, evaluates all rules in parallel for better performance.</param>
        /// <param name="observer">Optional observer for receiving async rule evaluation callbacks.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckAllAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            bool stopOnFirstFailure = false,
            bool runInParallel = false,
            IAsyncRuleExecutionObserver? observer = null,
            CancellationToken cancellationToken = default)
        {
            await CheckAllAsyncCore(context, rules, LegacyOptions(stopOnFirstFailure, runInParallel, observer), cancellationToken);
        }

        /// <summary>
        /// Checks all async rules with the supplied options against a fresh context and throws if any broken rule is
        /// at or above <see cref="AsyncRuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>. The overload is generic so that a bare
        /// <c>null</c> or <c>default</c> literal, which has no type to infer from, never selects it over the 2.x
        /// overloads: <c>CheckAllAsync(rules, default)</c> keeps meaning <c>stopOnFirstFailure: false</c>.</typeparam>
        /// <param name="rules">The async rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy, severity threshold and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold; broken rules below it are in <see cref="BusinessRuleValidationException.CollectedRules"/>.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckAllAsync<TOptions>(
            IEnumerable<IAsyncBusinessRule> rules,
            TOptions options,
            CancellationToken cancellationToken = default)
            where TOptions : AsyncRuleEvaluationOptions
        {
            return CheckAllAsyncCore(new BusinessRuleContext(), rules, options, cancellationToken);
        }

        /// <summary>
        /// Checks all async rules using context with the supplied options and throws if any broken rule is at or above
        /// <see cref="AsyncRuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>. The overload is generic so that a bare
        /// <c>null</c> or <c>default</c> literal, which has no type to infer from, never selects it over the 2.x
        /// overloads: <c>CheckAllAsync(context, rules, default)</c> keeps meaning <c>stopOnFirstFailure: false</c>.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy, severity threshold and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold; broken rules below it are in <see cref="BusinessRuleValidationException.CollectedRules"/>.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task CheckAllAsync<TOptions>(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            TOptions options,
            CancellationToken cancellationToken = default)
            where TOptions : AsyncRuleEvaluationOptions
        {
            return CheckAllAsyncCore(context, rules, options, cancellationToken);
        }

        /// <summary>
        /// Evaluates all async rules and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="rules">The async rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules with filtering capabilities.</returns>
        public static async Task<AsyncRuleEvaluationResult> EvaluateAllAsync(params IAsyncBusinessRule[] rules)
        {
            return await EvaluateAllAsyncCore(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions(), RuleSeverity.Info, cancellationToken: default);
        }

        /// <summary>
        /// Evaluates all async rules and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules with filtering capabilities.</returns>
        public static async Task<AsyncRuleEvaluationResult> EvaluateAllAsync(BusinessRuleContext context, params IAsyncBusinessRule[] rules)
        {
            return await EvaluateAllAsyncCore(context, rules, new AsyncRuleEvaluationOptions(), RuleSeverity.Info, cancellationToken: default);
        }

        /// <summary>
        /// Evaluates all async rules and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="rules">The async rules to evaluate.</param>
        /// <param name="runInParallel">If true, evaluates all rules in parallel for better performance.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules with filtering capabilities.</returns>
        public static async Task<AsyncRuleEvaluationResult> EvaluateAllAsync(
            IEnumerable<IAsyncBusinessRule> rules,
            bool runInParallel = false,
            CancellationToken cancellationToken = default)
        {
            return await EvaluateAllAsyncCore(new BusinessRuleContext(), rules, new AsyncRuleEvaluationOptions { RunInParallel = runInParallel }, RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Evaluates all async rules and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to evaluate.</param>
        /// <param name="runInParallel">If true, evaluates all rules in parallel for better performance.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules with filtering capabilities.</returns>
        public static async Task<AsyncRuleEvaluationResult> EvaluateAllAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            bool runInParallel = false,
            CancellationToken cancellationToken = default)
        {
            return await EvaluateAllAsyncCore(context, rules, new AsyncRuleEvaluationOptions { RunInParallel = runInParallel }, RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Evaluates all async rules with the supplied options against a fresh context and returns the result with
        /// its evaluation ledger. Never throws for broken rules: <see cref="AsyncRuleEvaluationOptions.ThrowOn"/> is
        /// ignored, faults follow <see cref="AsyncRuleEvaluationOptions.OnFault"/> and the observer receives every callback.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>. The overload is generic so that a bare
        /// <c>null</c> or <c>default</c> literal, which has no type to infer from, never selects it over the 2.x
        /// overloads: <c>EvaluateAllAsync(rules, default)</c> keeps meaning <c>runInParallel: false</c>.</typeparam>
        /// <param name="rules">The async rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TOptions>(
            IEnumerable<IAsyncBusinessRule> rules,
            TOptions options,
            CancellationToken cancellationToken = default)
            where TOptions : AsyncRuleEvaluationOptions
        {
            return EvaluateAllAsyncCore(new BusinessRuleContext(), rules, options, RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Evaluates all async rules using context with the supplied options and returns the result with its
        /// evaluation ledger. Never throws for broken rules: <see cref="AsyncRuleEvaluationOptions.ThrowOn"/> is
        /// ignored, faults follow <see cref="AsyncRuleEvaluationOptions.OnFault"/> and the observer receives every callback.
        /// </summary>
        /// <typeparam name="TOptions">Inferred from <paramref name="options"/>. The overload is generic so that a bare
        /// <c>null</c> or <c>default</c> literal, which has no type to infer from, never selects it over the 2.x
        /// overloads: <c>EvaluateAllAsync(context, rules, default)</c> keeps meaning <c>runInParallel: false</c>.</typeparam>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, parallel, fault policy and observer.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static Task<AsyncRuleEvaluationResult> EvaluateAllAsync<TOptions>(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            TOptions options,
            CancellationToken cancellationToken = default)
            where TOptions : AsyncRuleEvaluationOptions
        {
            return EvaluateAllAsyncCore(context, rules, options, RuleSeverity.Info, cancellationToken);
        }

        /// <summary>
        /// Checks only async rules that match the specified severity levels.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to filter and check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <param name="severities">The severity levels to include.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckBySeverityAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            CancellationToken cancellationToken = default,
            params RuleSeverity[] severities)
        {
            IEnumerable<IAsyncBusinessRule> filteredRules = rules.Where(r => severities.Contains(r.Severity));
            await CheckAllAsyncCore(context, filteredRules.ToArray(), LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken);
        }

        /// <summary>
        /// Checks only async rules that match the specified category.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to filter and check.</param>
        /// <param name="category">The category to filter by.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckByCategoryAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            string category,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<IAsyncBusinessRule> filteredRules = rules.Where(r => r.Category == category);
            await CheckAllAsyncCore(context, filteredRules.ToArray(), LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken);
        }

        /// <summary>
        /// Checks only async rules that have any of the specified tags.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to filter and check.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <param name="tags">The tags to filter by (case-insensitive).</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckByTagsAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            CancellationToken cancellationToken = default,
            params string[] tags)
        {
            HashSet<string> tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            IEnumerable<IAsyncBusinessRule> filteredRules = rules.Where(r => r.Tags.Any(t => tagSet.Contains(t)));
            await CheckAllAsyncCore(context, filteredRules.ToArray(), LegacyOptions(stopOnFirstFailure: false, runInParallel: false, observer: null), cancellationToken);
        }

        internal static AsyncRuleEvaluationOptions LegacyOptions(bool stopOnFirstFailure, bool runInParallel, IAsyncRuleExecutionObserver? observer)
        {
            return new AsyncRuleEvaluationOptions
            {
                StopOnFirstFailure = stopOnFirstFailure,
                RunInParallel = runInParallel,
                Observer = observer
            };
        }

        internal static async Task CheckAllAsyncCore(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            AsyncRuleEvaluationOptions options,
            CancellationToken cancellationToken)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            AsyncRuleEvaluationResult result = await EvaluateAllAsyncCore(context, rules, options, options.ThrowOn, cancellationToken);
            result.ThrowIfAtLeast(options.ThrowOn);
        }

        /// <summary>
        /// The single evaluation core. <paramref name="stopThreshold"/> is the lowest severity at which a broken rule
        /// stops a fail-fast batch: <c>ThrowOn</c> for <c>CheckAllAsync</c> (a rule that would not throw must not stop
        /// the search for one that would) and <see cref="RuleSeverity.Info"/> for <c>EvaluateAllAsync</c>.
        /// Fail-fast forces sequential evaluation even when parallel evaluation was requested.
        /// </summary>
        internal static async Task<AsyncRuleEvaluationResult> EvaluateAllAsyncCore(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            AsyncRuleEvaluationOptions options,
            RuleSeverity stopThreshold,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            IReadOnlyList<IAsyncBusinessRule> batch = rules as IReadOnlyList<IAsyncBusinessRule> ?? rules.ToList();
            IAsyncRuleExecutionObserver? observer = options.Observer;
            List<BusinessRuleResult> shadowResults = new List<BusinessRuleResult>();
            ShadowViolationSink? sink = ShadowViolationSink.Attach(context, out bool attachedSink);
            bool batchOpen = false;

            try
            {
                long batchStart = RuleTiming.Start();
                if (observer != null)
                {
                    await observer.OnBatchStartedAsync(batch.Count, cancellationToken);
                }

                batchOpen = true;

                List<RuleEvaluationEntry> entries = options.EffectiveParallel
                    ? await EvaluateParallelAsync(context, batch, options, sink, shadowResults, cancellationToken)
                    : await EvaluateSequentialAsync(context, batch, options, stopThreshold, sink, shadowResults, cancellationToken);

                AsyncRuleEvaluationResult result = new AsyncRuleEvaluationResult(entries, RuleTiming.ElapsedSince(batchStart), shadowResults);
                batchOpen = false;

                if (observer != null)
                {
                    await observer.OnBatchCompletedAsync(result, cancellationToken);
                }

                return result;
            }
            catch (Exception exception) when (batchOpen && observer != null)
            {
                await observer!.OnBatchAbortedAsync(exception, CancellationToken.None); // the filter guarantees a non-null observer
                throw;
            }
            finally
            {
                if (attachedSink)
                {
                    ShadowViolationSink.Detach(context);
                }
            }
        }

        private static async Task<List<RuleEvaluationEntry>> EvaluateSequentialAsync(
            BusinessRuleContext context,
            IReadOnlyList<IAsyncBusinessRule> batch,
            AsyncRuleEvaluationOptions options,
            RuleSeverity stopThreshold,
            ShadowViolationSink? sink,
            List<BusinessRuleResult> shadowResults,
            CancellationToken cancellationToken)
        {
            List<RuleEvaluationEntry> entries = new List<RuleEvaluationEntry>(batch.Count);

            int index = 0;
            for (; index < batch.Count; index++)
            {
                RuleEvaluationEntry entry = await EvaluateEntryAsync(index, batch[index], context, options, cancellationToken);
                entries.Add(entry);
                await ReportShadowViolationsAsync(sink, options.Observer, shadowResults, cancellationToken);

                if (options.StopOnFirstFailure && entry.Result != null && RuleSeverityThreshold.IsAtLeast(entry.Result.Severity, stopThreshold))
                {
                    index++;
                    break;
                }
            }

            for (; index < batch.Count; index++)
            {
                entries.Add(NotEvaluated(index, batch[index]));
            }

            return entries;
        }

        private static async Task<List<RuleEvaluationEntry>> EvaluateParallelAsync(
            BusinessRuleContext context,
            IReadOnlyList<IAsyncBusinessRule> batch,
            AsyncRuleEvaluationOptions options,
            ShadowViolationSink? sink,
            List<BusinessRuleResult> shadowResults,
            CancellationToken cancellationToken)
        {
            Task<RuleEvaluationEntry>[] tasks = new Task<RuleEvaluationEntry>[batch.Count];
            for (int index = 0; index < batch.Count; index++)
            {
                tasks[index] = EvaluateEntryAsync(index, batch[index], context, options, cancellationToken);
            }

            RuleEvaluationEntry[] entries = await Task.WhenAll(tasks);
            await ReportShadowViolationsAsync(sink, options.Observer, shadowResults, cancellationToken);
            return new List<RuleEvaluationEntry>(entries);
        }

        /// <summary>
        /// Hands every shadow violation recorded since the last call to the observer and the batch's shadow results.
        /// </summary>
        private static async Task ReportShadowViolationsAsync(
            ShadowViolationSink? sink,
            IAsyncRuleExecutionObserver? observer,
            List<BusinessRuleResult> shadowResults,
            CancellationToken cancellationToken)
        {
            if (sink == null)
            {
                return;
            }

            foreach (ShadowViolation violation in sink.Drain())
            {
                shadowResults.Add(violation.Result);
                if (observer == null)
                {
                    continue;
                }

                IAsyncBusinessRule gatedRule = violation.AsAsyncRule();
                await observer.OnShadowViolationAsync(gatedRule, violation.Result, cancellationToken);
                if (violation.Fault != null)
                {
                    await observer.OnShadowFaultAsync(gatedRule, violation.Fault, cancellationToken);
                }
            }
        }

        private static async Task<RuleEvaluationEntry> EvaluateEntryAsync(
            int order,
            IAsyncBusinessRule rule,
            BusinessRuleContext context,
            AsyncRuleEvaluationOptions options,
            CancellationToken cancellationToken)
        {
            if (rule == null)
            {
                throw new ArgumentException(CheckerText.NullRuleAt(order), CheckerText.RulesParameterName);
            }

            cancellationToken.ThrowIfCancellationRequested();

            IAsyncRuleExecutionObserver? observer = options.Observer;
            if (observer != null)
            {
                await observer.OnBeforeEvaluateAsync(rule, cancellationToken);
            }

            long start = RuleTiming.Start();
            BusinessRuleResult? result;
            try
            {
                result = await AsyncBusinessRuleWalker.EvaluateNodeAsync(rule, context, null, cancellationToken);
            }
            catch (OperationCanceledException cancellation)
            {
                if (observer != null)
                {
                    await observer.OnRuleCancelledAsync(rule, cancellation, CancellationToken.None);
                }

                throw;
            }
            catch (Exception exception)
            {
                TimeSpan faultElapsed = RuleTiming.ElapsedSince(start);
                Exception fault = ContextReadFault.Wrap(rule, exception);
                if (observer != null)
                {
                    await observer.OnRuleFaultedAsync(rule, fault, cancellationToken);
                }

                if (options.OnFault == FaultPolicy.Throw)
                {
                    if (ReferenceEquals(fault, exception))
                    {
                        throw;
                    }

                    throw fault;
                }

                return await RecordFaultAsync(order, rule, fault, faultElapsed, options, cancellationToken);
            }

            TimeSpan elapsed = RuleTiming.ElapsedSince(start);
            bool isBroken = result != null;

            if (observer != null)
            {
                await observer.OnAfterEvaluateAsync(rule, result, cancellationToken);
                await observer.OnAfterEvaluateAsync(rule, isBroken, elapsed, cancellationToken);

                if (result != null)
                {
                    await observer.OnRuleBrokenAsync(rule, result, cancellationToken);
                }
            }

            return new RuleEvaluationEntry(order, rule, isBroken ? RuleOutcome.Broken : RuleOutcome.Passed, result, null, elapsed);
        }

        private static async Task<RuleEvaluationEntry> RecordFaultAsync(
            int order,
            IAsyncBusinessRule rule,
            Exception exception,
            TimeSpan elapsed,
            AsyncRuleEvaluationOptions options,
            CancellationToken cancellationToken)
        {
            BusinessRuleResult? result = null;

            if (options.OnFault == FaultPolicy.TreatAsBroken)
            {
                result = RuleFaultResult.Create(rule, exception);
                if (options.Observer != null)
                {
                    await options.Observer.OnRuleBrokenAsync(rule, result, cancellationToken);
                }
            }

            return new RuleEvaluationEntry(order, rule, RuleOutcome.Faulted, result, exception, elapsed);
        }

        private static RuleEvaluationEntry NotEvaluated(int order, IAsyncBusinessRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentException(CheckerText.NullRuleAt(order), CheckerText.RulesParameterName);
            }

            return new RuleEvaluationEntry(order, rule, RuleOutcome.NotEvaluated, null, null, TimeSpan.Zero);
        }
    }
}
