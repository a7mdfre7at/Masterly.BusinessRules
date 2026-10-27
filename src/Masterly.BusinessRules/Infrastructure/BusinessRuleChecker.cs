using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Static utility class for batch validation of synchronous business rules.
    /// Provides methods for checking multiple rules at once, filtering by criteria, and detailed evaluation.
    /// Composed rules (groups, composites, And/Or/Not) are evaluated exactly once and their broken members are
    /// surfaced in <see cref="BusinessRuleResult.Details"/> of the reported result.
    /// Every overload runs through one core that records an evaluation ledger (<see cref="RuleEvaluationResult.Entries"/>);
    /// the overloads taking <see cref="RuleEvaluationOptions"/> add a fault policy, a severity threshold and observer
    /// callbacks from both <c>CheckAll</c> and <c>EvaluateAll</c>.
    /// A rule that fails to read the context (<see cref="ContextKeyNotFoundException"/> / <see cref="ContextValueTypeMismatchException"/>)
    /// is reported as a <see cref="BusinessRuleContextException"/> naming the rule's <c>Code</c>, with the original exception preserved
    /// as its inner exception; the fault policy then applies to the wrapped exception.
    /// Violations observed by <see cref="RuleRolloutMode.Shadow"/> gated rules are collected on
    /// <see cref="RuleEvaluationResult.ShadowResults"/> and reported through <see cref="IRuleExecutionObserver.OnShadowViolation"/>
    /// when the batch is evaluated with a context.
    /// </summary>
    /// <example>
    /// <code>
    /// // Basic usage
    /// BusinessRuleChecker.CheckAll(rule1, rule2, rule3);
    ///
    /// // With context and fail-fast
    /// BusinessRuleChecker.CheckAll(rules, stopOnFirstFailure: true, context: ctx);
    ///
    /// // Evaluate without throwing
    /// RuleEvaluationResult result = BusinessRuleChecker.EvaluateAll(rules);
    /// if (result.HasErrors)
    ///     Console.WriteLine(result.Errors.First().Message);
    ///
    /// // Options object: record faults, throw only at Error
    /// BusinessRuleChecker.CheckAll(rules, ctx, new RuleEvaluationOptions { OnFault = FaultPolicy.Record, ThrowOn = RuleSeverity.Error });
    /// </code>
    /// </example>
    public static partial class BusinessRuleChecker
    {
        /// <summary>
        /// Checks all rules and throws if any are broken.
        /// </summary>
        /// <param name="rules">The rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll(params IBusinessRule[] rules)
        {
            CheckAllCore(rules, null, LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Checks all rules with fail-fast option and throws if any are broken.
        /// </summary>
        /// <param name="stopOnFirstFailure">If true, stops checking after the first broken rule.</param>
        /// <param name="rules">The rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll(bool stopOnFirstFailure, params IBusinessRule[] rules)
        {
            CheckAllCore(rules, null, LegacyOptions(stopOnFirstFailure, observer: null));
        }

        /// <summary>
        /// Checks all rules using context and throws if any are broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll(BusinessRuleContext context, params IBusinessRule[] rules)
        {
            CheckAllCore(rules, context, LegacyOptions(stopOnFirstFailure: false, observer: null));
        }

        /// <summary>
        /// Checks all rules using context with fail-fast option and throws if any are broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="stopOnFirstFailure">If true, stops checking after the first broken rule.</param>
        /// <param name="rules">The rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll(BusinessRuleContext context, bool stopOnFirstFailure, params IBusinessRule[] rules)
        {
            CheckAllCore(rules, context, LegacyOptions(stopOnFirstFailure, observer: null));
        }

        /// <summary>
        /// Checks all rules with options for fail-fast behavior and observer notifications.
        /// </summary>
        /// <param name="rules">The rules to check.</param>
        /// <param name="stopOnFirstFailure">If true, stops checking after the first broken rule.</param>
        /// <param name="observer">Optional observer for receiving rule evaluation callbacks.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll(
            IEnumerable<IBusinessRule> rules,
            bool stopOnFirstFailure = false,
            IRuleExecutionObserver? observer = null,
            BusinessRuleContext? context = null)
        {
            CheckAllCore(rules, context, LegacyOptions(stopOnFirstFailure, observer));
        }

        /// <summary>
        /// Checks all rules with the supplied options and throws if any broken rule is at or above
        /// <see cref="RuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <param name="rules">The rules to check.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy, severity threshold and observer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold; broken rules below it are in <see cref="BusinessRuleValidationException.CollectedRules"/>.</exception>
        public static void CheckAll(IEnumerable<IBusinessRule> rules, RuleEvaluationOptions options)
        {
            CheckAllCore(rules, null, options);
        }

        /// <summary>
        /// Checks all rules using context with the supplied options and throws if any broken rule is at or above
        /// <see cref="RuleEvaluationOptions.ThrowOn"/>.
        /// </summary>
        /// <param name="rules">The rules to check.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy, severity threshold and observer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="BusinessRuleValidationException">Thrown when a broken rule is at or above the threshold; broken rules below it are in <see cref="BusinessRuleValidationException.CollectedRules"/>.</exception>
        public static void CheckAll(IEnumerable<IBusinessRule> rules, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            CheckAllCore(rules, context, options);
        }

        /// <summary>
        /// Evaluates all rules and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="rules">The rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules.</returns>
        public static RuleEvaluationResult EvaluateAll(params IBusinessRule[] rules)
        {
            return EvaluateAllCore(rules, null, new RuleEvaluationOptions(), RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates all rules with context and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules.</returns>
        public static RuleEvaluationResult EvaluateAll(BusinessRuleContext context, params IBusinessRule[] rules)
        {
            return EvaluateAllCore(rules, context, new RuleEvaluationOptions(), RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates all rules and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="rules">The rules to evaluate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <returns>A result object containing broken and passed rules with filtering capabilities.</returns>
        public static RuleEvaluationResult EvaluateAll(
            IEnumerable<IBusinessRule> rules,
            BusinessRuleContext? context = null)
        {
            return EvaluateAllCore(rules, context, new RuleEvaluationOptions(), RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates all rules with the supplied options and returns the result with its evaluation ledger.
        /// Never throws for broken rules: <see cref="RuleEvaluationOptions.ThrowOn"/> is ignored, faults follow
        /// <see cref="RuleEvaluationOptions.OnFault"/> and the observer receives every callback.
        /// </summary>
        /// <param name="rules">The rules to evaluate.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy and observer.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static RuleEvaluationResult EvaluateAll(IEnumerable<IBusinessRule> rules, RuleEvaluationOptions options)
        {
            return EvaluateAllCore(rules, null, options, RuleSeverity.Info);
        }

        /// <summary>
        /// Evaluates all rules using context with the supplied options and returns the result with its evaluation ledger.
        /// Never throws for broken rules: <see cref="RuleEvaluationOptions.ThrowOn"/> is ignored, faults follow
        /// <see cref="RuleEvaluationOptions.OnFault"/> and the observer receives every callback.
        /// </summary>
        /// <param name="rules">The rules to evaluate.</param>
        /// <param name="context">Optional context containing data for rule evaluation.</param>
        /// <param name="options">The evaluation options: fail-fast, fault policy and observer.</param>
        /// <returns>A result object containing broken and passed rules and the per-rule ledger.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or <paramref name="options"/> is null.</exception>
        public static RuleEvaluationResult EvaluateAll(IEnumerable<IBusinessRule> rules, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            return EvaluateAllCore(rules, context, options, RuleSeverity.Info);
        }

        /// <summary>
        /// Checks only rules that match the specified severity levels.
        /// </summary>
        /// <param name="rules">The rules to filter and check.</param>
        /// <param name="severities">The severity levels to include.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public static void CheckBySeverity(
            IEnumerable<IBusinessRule> rules,
            params RuleSeverity[] severities)
        {
            IEnumerable<IBusinessRule> filteredRules = rules.Where(r => severities.Contains(r.Severity));
            CheckAll(filteredRules.ToArray());
        }

        /// <summary>
        /// Checks only rules that match the specified category.
        /// </summary>
        /// <param name="rules">The rules to filter and check.</param>
        /// <param name="category">The category to filter by.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public static void CheckByCategory(
            IEnumerable<IBusinessRule> rules,
            string category)
        {
            IEnumerable<IBusinessRule> filteredRules = rules.Where(r => r.Category == category);
            CheckAll(filteredRules.ToArray());
        }

        /// <summary>
        /// Checks only rules that have any of the specified tags.
        /// </summary>
        /// <param name="rules">The rules to filter and check.</param>
        /// <param name="tags">The tags to filter by (case-insensitive).</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any matching rules are broken.</exception>
        public static void CheckByTags(
            IEnumerable<IBusinessRule> rules,
            params string[] tags)
        {
            HashSet<string> tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            IEnumerable<IBusinessRule> filteredRules = rules.Where(r => r.Tags.Any(t => tagSet.Contains(t)));
            CheckAll(filteredRules.ToArray());
        }

        private static RuleEvaluationOptions LegacyOptions(bool stopOnFirstFailure, IRuleExecutionObserver? observer)
        {
            return new RuleEvaluationOptions
            {
                StopOnFirstFailure = stopOnFirstFailure,
                Observer = observer
            };
        }

        private static void CheckAllCore(IEnumerable<IBusinessRule> rules, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            RuleEvaluationResult result = EvaluateAllCore(rules, context, options, options.ThrowOn);
            result.ThrowIfAtLeast(options.ThrowOn);
        }

        /// <summary>
        /// The single evaluation core. <paramref name="stopThreshold"/> is the lowest severity at which a broken rule
        /// stops a fail-fast batch: <c>ThrowOn</c> for <c>CheckAll</c> (a rule that would not throw must not stop the
        /// search for one that would) and <see cref="RuleSeverity.Info"/> for <c>EvaluateAll</c>.
        /// </summary>
        private static RuleEvaluationResult EvaluateAllCore(
            IEnumerable<IBusinessRule> rules,
            BusinessRuleContext? context,
            RuleEvaluationOptions options,
            RuleSeverity stopThreshold)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            IReadOnlyList<IBusinessRule> batch = rules as IReadOnlyList<IBusinessRule> ?? rules.ToList();
            IRuleExecutionObserver? observer = options.Observer;
            List<RuleEvaluationEntry> entries = new List<RuleEvaluationEntry>(batch.Count);
            List<BusinessRuleResult> shadowResults = new List<BusinessRuleResult>();
            ShadowViolationSink? sink = ShadowViolationSink.Attach(context, out bool attachedSink);
            bool batchOpen = false;

            try
            {
                long batchStart = RuleTiming.Start();
                observer?.OnBatchStarted(batch.Count);
                batchOpen = true;

                int index = 0;
                for (; index < batch.Count; index++)
                {
                    RuleEvaluationEntry entry = EvaluateEntry(index, batch[index], context, options);
                    entries.Add(entry);
                    ReportShadowViolations(sink, observer, shadowResults);

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

                RuleEvaluationResult result = new RuleEvaluationResult(entries, RuleTiming.ElapsedSince(batchStart), shadowResults);
                batchOpen = false;
                observer?.OnBatchCompleted(result);
                return result;
            }
            catch (Exception exception) when (batchOpen && observer != null)
            {
                observer!.OnBatchAborted(exception); // the filter guarantees a non-null observer
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

        /// <summary>
        /// Hands every shadow violation recorded since the last call to the observer and the batch's shadow results.
        /// </summary>
        private static void ReportShadowViolations(ShadowViolationSink? sink, IRuleExecutionObserver? observer, List<BusinessRuleResult> shadowResults)
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

                IBusinessRule gatedRule = violation.AsSyncRule();
                observer.OnShadowViolation(gatedRule, violation.Result);
                if (violation.Fault != null)
                {
                    observer.OnShadowFault(gatedRule, violation.Fault);
                }
            }
        }

        private static RuleEvaluationEntry EvaluateEntry(int order, IBusinessRule rule, BusinessRuleContext? context, RuleEvaluationOptions options)
        {
            if (rule == null)
            {
                throw new ArgumentException(CheckerText.NullRuleAt(order), CheckerText.RulesParameterName);
            }

            IRuleExecutionObserver? observer = options.Observer;
            observer?.OnBeforeEvaluate(rule);

            long start = RuleTiming.Start();
            BusinessRuleResult? result;
            try
            {
                result = BusinessRuleWalker.EvaluateNode(rule, context, null);
            }
            catch (OperationCanceledException cancellation)
            {
                observer?.OnRuleCancelled(rule, cancellation);
                throw;
            }
            catch (Exception exception)
            {
                TimeSpan faultElapsed = RuleTiming.ElapsedSince(start);
                Exception fault = ContextReadFault.Wrap(rule, exception);
                observer?.OnRuleFaulted(rule, fault);

                if (options.OnFault == FaultPolicy.Throw)
                {
                    if (ReferenceEquals(fault, exception))
                    {
                        throw;
                    }

                    throw fault;
                }

                return RecordFault(order, rule, fault, faultElapsed, options);
            }

            TimeSpan elapsed = RuleTiming.ElapsedSince(start);
            bool isBroken = result != null;

            if (observer != null)
            {
                observer.OnAfterEvaluate(rule, result);
                observer.OnAfterEvaluate(rule, isBroken, elapsed);

                if (result != null)
                {
                    observer.OnRuleBroken(rule, result);
                }
            }

            return new RuleEvaluationEntry(order, rule, isBroken ? RuleOutcome.Broken : RuleOutcome.Passed, result, null, elapsed);
        }

        private static RuleEvaluationEntry RecordFault(int order, IBusinessRule rule, Exception exception, TimeSpan elapsed, RuleEvaluationOptions options)
        {
            BusinessRuleResult? result = null;

            if (options.OnFault == FaultPolicy.TreatAsBroken)
            {
                result = RuleFaultResult.Create(rule, exception);
                options.Observer?.OnRuleBroken(rule, result);
            }

            return new RuleEvaluationEntry(order, rule, RuleOutcome.Faulted, result, exception, elapsed);
        }

        private static RuleEvaluationEntry NotEvaluated(int order, IBusinessRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentException(CheckerText.NullRuleAt(order), CheckerText.RulesParameterName);
            }

            return new RuleEvaluationEntry(order, rule, RuleOutcome.NotEvaluated, null, null, TimeSpan.Zero);
        }
    }
}
