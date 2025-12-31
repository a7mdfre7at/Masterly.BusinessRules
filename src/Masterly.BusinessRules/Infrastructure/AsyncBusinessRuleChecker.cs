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
    /// </code>
    /// </example>
    public static class AsyncBusinessRuleChecker
    {
        /// <summary>
        /// Checks all async rules sequentially and throws if any are broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The async rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CheckAllAsync(
            BusinessRuleContext context,
            params IAsyncBusinessRule[] rules)
        {
            await CheckAllAsync(context, rules, stopOnFirstFailure: false, runInParallel: false);
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
            List<BusinessRuleResult> brokenRules = new List<BusinessRuleResult>();

            if (runInParallel && !stopOnFirstFailure)
            {
                brokenRules = await EvaluateParallelAsync(context, rules, observer, cancellationToken);
            }
            else
            {
                brokenRules = await EvaluateSequentialAsync(context, rules, stopOnFirstFailure, observer, cancellationToken);
            }

            if (brokenRules.Any())
            {
                throw new BusinessRuleValidationException(brokenRules);
            }
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
            List<BusinessRuleResult> brokenRules = new List<BusinessRuleResult>();
            List<IAsyncBusinessRule> passedRules = new List<IAsyncBusinessRule>();
            List<IAsyncBusinessRule> rulesList = rules.ToList();

            if (runInParallel)
            {
                IEnumerable<Task<(IAsyncBusinessRule rule, BusinessRuleResult? result)>> tasks = rulesList.Select(async rule =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    BusinessRuleResult? result = await rule.EvaluateAsync(context, cancellationToken);
                    return (rule, result);
                });

                (IAsyncBusinessRule rule, BusinessRuleResult? result)[] results = await Task.WhenAll(tasks);
                foreach ((IAsyncBusinessRule rule, BusinessRuleResult? result) in results)
                {
                    if (result != null)
                        brokenRules.Add(result);
                    else
                        passedRules.Add(rule);
                }
            }
            else
            {
                foreach (IAsyncBusinessRule rule in rulesList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    BusinessRuleResult? result = await rule.EvaluateAsync(context, cancellationToken);
                    if (result != null)
                        brokenRules.Add(result);
                    else
                        passedRules.Add(rule);
                }
            }

            return new AsyncRuleEvaluationResult(brokenRules, passedRules);
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
            await CheckAllAsync(context, filteredRules.ToArray());
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
            await CheckAllAsync(context, filteredRules.ToArray());
        }

        private static async Task<List<BusinessRuleResult>> EvaluateSequentialAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            bool stopOnFirstFailure,
            IAsyncRuleExecutionObserver? observer,
            CancellationToken cancellationToken)
        {
            List<BusinessRuleResult> brokenRules = new List<BusinessRuleResult>();

            foreach (IAsyncBusinessRule rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (observer != null)
                    await observer.OnBeforeEvaluateAsync(rule, cancellationToken);

                BusinessRuleResult? result = await rule.EvaluateAsync(context, cancellationToken);

                if (result != null)
                {
                    brokenRules.Add(result);

                    if (observer != null)
                    {
                        await observer.OnAfterEvaluateAsync(rule, result, cancellationToken);
                        await observer.OnRuleBrokenAsync(rule, result, cancellationToken);
                    }

                    if (stopOnFirstFailure)
                        break;
                }
                else if (observer != null)
                {
                    await observer.OnAfterEvaluateAsync(rule, null, cancellationToken);
                }
            }

            return brokenRules;
        }

        private static async Task<List<BusinessRuleResult>> EvaluateParallelAsync(
            BusinessRuleContext context,
            IEnumerable<IAsyncBusinessRule> rules,
            IAsyncRuleExecutionObserver? observer,
            CancellationToken cancellationToken)
        {
            IEnumerable<Task<BusinessRuleResult?>> tasks = rules.Select(async rule =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (observer != null)
                    await observer.OnBeforeEvaluateAsync(rule, cancellationToken);

                BusinessRuleResult? result = await rule.EvaluateAsync(context, cancellationToken);

                if (observer != null)
                {
                    await observer.OnAfterEvaluateAsync(rule, result, cancellationToken);
                    if (result != null)
                        await observer.OnRuleBrokenAsync(rule, result, cancellationToken);
                }

                return result;
            });

            BusinessRuleResult?[] results = await Task.WhenAll(tasks);
            return results.Where(r => r != null).Cast<BusinessRuleResult>().ToList();
        }
    }
}
