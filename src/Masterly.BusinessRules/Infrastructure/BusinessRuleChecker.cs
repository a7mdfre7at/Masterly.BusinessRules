using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Static utility class for batch validation of synchronous business rules.
    /// Provides methods for checking multiple rules at once, filtering by criteria, and detailed evaluation.
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
    /// </code>
    /// </example>
    public static class BusinessRuleChecker
    {
        /// <summary>
        /// Checks all rules and throws if any are broken.
        /// </summary>
        /// <param name="rules">The rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll(params IBusinessRule[] rules)
        {
            CheckAll(rules, stopOnFirstFailure: false, observer: null, context: null);
        }

        /// <summary>
        /// Checks all rules using context and throws if any are broken.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The rules to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when any rules are broken.</exception>
        public static void CheckAll(BusinessRuleContext context, params IBusinessRule[] rules)
        {
            CheckAll(rules, stopOnFirstFailure: false, observer: null, context: context);
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
            List<BusinessRuleResult> brokenRules = new List<BusinessRuleResult>();

            foreach (IBusinessRule rule in rules)
            {
                observer?.OnBeforeEvaluate(rule);

                bool isBroken = context != null ? rule.IsBroken(context) : rule.IsBroken();

                if (isBroken)
                {
                    BusinessRuleResult result = rule.Evaluate();
                    brokenRules.Add(result);
                    observer?.OnAfterEvaluate(rule, result);
                    observer?.OnRuleBroken(rule, result);

                    if (stopOnFirstFailure)
                        break;
                }
                else
                {
                    observer?.OnAfterEvaluate(rule, null);
                }
            }

            if (brokenRules.Any())
            {
                throw new BusinessRuleValidationException(brokenRules);
            }
        }

        /// <summary>
        /// Evaluates all rules and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="rules">The rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules.</returns>
        public static RuleEvaluationResult EvaluateAll(params IBusinessRule[] rules)
        {
            return EvaluateAll(rules, context: null);
        }

        /// <summary>
        /// Evaluates all rules with context and returns detailed results without throwing exceptions.
        /// </summary>
        /// <param name="context">The context containing data for rule evaluation.</param>
        /// <param name="rules">The rules to evaluate.</param>
        /// <returns>A result object containing broken and passed rules.</returns>
        public static RuleEvaluationResult EvaluateAll(BusinessRuleContext context, params IBusinessRule[] rules)
        {
            return EvaluateAll(rules, context: context);
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
            List<BusinessRuleResult> brokenRules = new List<BusinessRuleResult>();
            List<IBusinessRule> passedRules = new List<IBusinessRule>();

            foreach (IBusinessRule rule in rules)
            {
                bool isBroken = context != null ? rule.IsBroken(context) : rule.IsBroken();

                if (isBroken)
                {
                    brokenRules.Add(rule.Evaluate());
                }
                else
                {
                    passedRules.Add(rule);
                }
            }

            return new RuleEvaluationResult(brokenRules, passedRules);
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
    }
}